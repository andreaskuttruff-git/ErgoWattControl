using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;

namespace ErgoWattControl;

/// <summary>
/// FTMS over Windows' Bluetooth LE stack (WinRT): scans actively for advertisements of the Fitness Machine Service,
/// prefers one named "ErgoBridge", connects and switches on the notifications and indications the app needs.
/// </summary>
internal sealed class WinRtFtmsLink : IFtmsLink
{
    public const string BridgeName = "ErgoBridge";
    private static readonly TimeSpan ScanTime = TimeSpan.FromSeconds(8);
    /// <summary>After the first FTMS trainer turned up, wait this much longer for an ErgoBridge.</summary>
    private static readonly TimeSpan PreferBridgeTime = TimeSpan.FromSeconds(2);

    private static readonly Guid FtmsUuid = BluetoothUuidHelper.FromShortId(FtmsProtocol.FitnessMachineService);

    public async Task<IFtmsConnection?> ConnectAsync(CancellationToken cancel)
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(cancel);
        if (adapter is null || !adapter.IsLowEnergySupported)
            throw new FtmsLinkException(L.T("Ftms_NoAdapter"), "no Bluetooth LE adapter");
        // IsLowEnergySupported describes the hardware; with Bluetooth switched off in Windows the radio is off.
        var radio = await adapter.GetRadioAsync().AsTask(cancel);
        if (radio is not null && radio.State != RadioState.On)
            throw new FtmsLinkException(L.T("Ftms_RadioOff"), $"Bluetooth radio is {radio.State}");

        if (await ScanAsync(cancel) is not { } found)
            return null;

        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(found.Address, found.AddressType).AsTask(cancel)
            ?? throw new IOException($"device {found.Address:X12} not reachable");
        try
        {
            return await WinRtFtmsConnection.OpenAsync(device, found.Name, cancel);
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private static async Task<Found?> ScanAsync(CancellationToken cancel)
    {
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(FtmsUuid);
        var gate = new object();
        Found? first = null;
        var bridgeFound = new TaskCompletionSource<Found>(TaskCreationOptions.RunContinuationsAsynchronously);
        var anyFound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aborted = new TaskCompletionSource<BluetoothError>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Received += (_, e) =>
        {
            var name = e.Advertisement.LocalName ?? "";
            var found = new Found(e.BluetoothAddress, e.BluetoothAddressType, name.Length > 0 ? name : $"{e.BluetoothAddress:X12}");
            lock (gate)
            {
                if (name == BridgeName)
                    bridgeFound.TrySetResult(found);
                first ??= found;
            }
            anyFound.TrySetResult();
        };
        // The watcher doesn't throw when the radio is off or taken: it stops with an error instead.
        watcher.Stopped += (_, e) =>
        {
            if (e.Error != BluetoothError.Success)
                aborted.TrySetResult(e.Error);
        };
        watcher.Start();
        try
        {
            var scan = Task.Delay(ScanTime, cancel);
            await Task.WhenAny(anyFound.Task, aborted.Task, scan);
            cancel.ThrowIfCancellationRequested();
            if (aborted.Task.IsCompleted && !anyFound.Task.IsCompleted)
                throw new FtmsLinkException(L.T("Ftms_RadioOff"), $"Bluetooth scan stopped ({aborted.Task.Result})");
            if (!anyFound.Task.IsCompleted)
                return null;
            await Task.WhenAny(bridgeFound.Task, Task.Delay(PreferBridgeTime, cancel));
            cancel.ThrowIfCancellationRequested();
            if (bridgeFound.Task.IsCompleted)
                return bridgeFound.Task.Result;
            lock (gate)
                return first;
        }
        finally
        {
            watcher.Stop();
        }
    }

    private sealed record Found(ulong Address, BluetoothAddressType AddressType, string Name);
}

internal sealed class WinRtFtmsConnection : IFtmsConnection
{
    private readonly BluetoothLEDevice _device;
    private readonly GattDeviceService _ftms;
    private readonly GattDeviceService? _bridge;
    private readonly GattCharacteristic _indoorBikeData;
    private readonly GattCharacteristic _controlPoint;
    private readonly GattCharacteristic? _powerRange;
    private readonly GattCharacteristic? _bridgeStatus;
    private int _disposed;

    public string Name { get; }
    public bool IsErgoBridge => _bridgeStatus is not null;

    public event Action<byte[]>? IndoorBikeData;
    public event Action<byte[]>? ControlPointResponse;
    public event Action<byte[]>? BridgeStatus;
    public event Action? Lost;

    private WinRtFtmsConnection(BluetoothLEDevice device, string name, GattDeviceService ftms, GattDeviceService? bridge,
        GattCharacteristic indoorBikeData, GattCharacteristic controlPoint, GattCharacteristic? powerRange,
        GattCharacteristic? bridgeStatus)
    {
        _device = device;
        Name = name;
        _ftms = ftms;
        _bridge = bridge;
        _indoorBikeData = indoorBikeData;
        _controlPoint = controlPoint;
        _powerRange = powerRange;
        _bridgeStatus = bridgeStatus;
    }

    public static async Task<WinRtFtmsConnection> OpenAsync(BluetoothLEDevice device, string name, CancellationToken cancel)
    {
        GattDeviceService? ftms = null, bridge = null;
        GattCharacteristic ibd, cp;
        GattCharacteristic? range, status;
        try
        {
            (ftms, bridge) = await ServicesAsync(device, cancel);
            if (ftms is null)
                throw new IOException("no Fitness Machine Service");

            ibd = await CharacteristicAsync(ftms, BluetoothUuidHelper.FromShortId(FtmsProtocol.IndoorBikeDataChar), cancel)
                ?? throw new IOException("no Indoor Bike Data");
            cp = await CharacteristicAsync(ftms, BluetoothUuidHelper.FromShortId(FtmsProtocol.ControlPointChar), cancel)
                ?? throw new IOException("no control point");
            range = await CharacteristicAsync(ftms, BluetoothUuidHelper.FromShortId(FtmsProtocol.SupportedPowerRangeChar), cancel);
            status = bridge is null ? null : await CharacteristicAsync(bridge, FtmsProtocol.BridgeStatusChar, cancel);
        }
        catch
        {
            // A held service keeps the GATT session (and the trainer's connection) open until the GC runs.
            ftms?.Dispose();
            bridge?.Dispose();
            throw;
        }

        var connection = new WinRtFtmsConnection(device, name, ftms, bridge, ibd, cp, range, status);
        try
        {
            ibd.ValueChanged += connection.OnIndoorBikeData;
            cp.ValueChanged += connection.OnControlPoint;
            await SubscribeAsync(ibd, GattClientCharacteristicConfigurationDescriptorValue.Notify, cancel);
            await SubscribeAsync(cp, GattClientCharacteristicConfigurationDescriptorValue.Indicate, cancel);
            if (status is not null)
            {
                status.ValueChanged += connection.OnBridgeStatus;
                await SubscribeAsync(status, GattClientCharacteristicConfigurationDescriptorValue.Notify, cancel);
            }
            device.ConnectionStatusChanged += connection.OnConnectionStatusChanged;
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The FTMS and the bridge service from one uncached discovery of all services. Uncached (a fresh firmware may have
    /// changed them), but only once: a second uncached query right after the first is refused by Windows with
    /// 0x80070016 (ERROR_BAD_COMMAND) on an ESP32 – measured with the ErgoBridge on 2026-11-28.
    /// </summary>
    private static async Task<(GattDeviceService? Ftms, GattDeviceService? Bridge)> ServicesAsync(BluetoothLEDevice device,
        CancellationToken cancel)
    {
        var result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancel);
        if (result.Status != GattCommunicationStatus.Success)
            throw new IOException($"service query failed ({result.Status})");
        var ftmsUuid = BluetoothUuidHelper.FromShortId(FtmsProtocol.FitnessMachineService);
        GattDeviceService? ftms = null, bridge = null;
        foreach (var service in result.Services)
        {
            if (ftms is null && service.Uuid == ftmsUuid)
                ftms = service;
            else if (bridge is null && service.Uuid == FtmsProtocol.BridgeService)
                bridge = service;
            else
                service.Dispose();
        }
        return (ftms, bridge);
    }

    private static async Task<GattCharacteristic?> CharacteristicAsync(GattDeviceService service, Guid uuid, CancellationToken cancel)
    {
        // Cached: the services were just discovered uncached, and Windows fetches what isn't cached yet from the device.
        var result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Cached).AsTask(cancel);
        if (result.Status != GattCommunicationStatus.Success)
            throw new IOException($"characteristic query failed ({result.Status})");
        return result.Characteristics.Count > 0 ? result.Characteristics[0] : null;
    }

    private static async Task SubscribeAsync(GattCharacteristic characteristic,
        GattClientCharacteristicConfigurationDescriptorValue kind, CancellationToken cancel)
    {
        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(kind).AsTask(cancel);
        if (status != GattCommunicationStatus.Success)
            throw new IOException($"subscribing to {characteristic.Uuid} failed ({status})");
    }

    public async Task<byte[]?> ReadPowerRangeAsync(CancellationToken cancel) =>
        _powerRange is null ? null : await ReadAsync(_powerRange, cancel);

    public async Task<byte[]?> ReadBridgeStatusAsync(CancellationToken cancel) =>
        _bridgeStatus is null ? null : await ReadAsync(_bridgeStatus, cancel);

    private static async Task<byte[]> ReadAsync(GattCharacteristic characteristic, CancellationToken cancel)
    {
        var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cancel);
        if (result.Status != GattCommunicationStatus.Success)
            throw new IOException($"reading {characteristic.Uuid} failed ({result.Status})");
        return result.Value.ToArray();
    }

    public async Task WriteControlPointAsync(byte[] request, CancellationToken cancel)
    {
        var result = await _controlPoint.WriteValueWithResultAsync(request.AsBuffer(), GattWriteOption.WriteWithResponse)
            .AsTask(cancel);
        if (result.Status != GattCommunicationStatus.Success)
            throw new IOException($"control point write failed ({result.Status}, protocol error {result.ProtocolError})");
    }

    private void OnIndoorBikeData(GattCharacteristic _, GattValueChangedEventArgs e) =>
        IndoorBikeData?.Invoke(e.CharacteristicValue.ToArray());

    private void OnControlPoint(GattCharacteristic _, GattValueChangedEventArgs e) =>
        ControlPointResponse?.Invoke(e.CharacteristicValue.ToArray());

    private void OnBridgeStatus(GattCharacteristic _, GattValueChangedEventArgs e) =>
        BridgeStatus?.Invoke(e.CharacteristicValue.ToArray());

    private void OnConnectionStatusChanged(BluetoothLEDevice device, object _)
    {
        if (device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            Lost?.Invoke();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _indoorBikeData.ValueChanged -= OnIndoorBikeData;
        _controlPoint.ValueChanged -= OnControlPoint;
        if (_bridgeStatus is not null)
            _bridgeStatus.ValueChanged -= OnBridgeStatus;
        _ftms.Dispose();
        _bridge?.Dispose();
        _device.Dispose();
    }
}
