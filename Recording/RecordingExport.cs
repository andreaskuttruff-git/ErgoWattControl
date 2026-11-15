using System.Globalization;
using System.Text;
using Fit = Dynastream.Fit;

namespace ErgoWattControl;

/// <summary>
/// Exports a <see cref="RecordingFile"/> for other programs: FIT for Strava, Garmin Connect, intervals.icu,
/// TrainingPeaks (a route as a virtual ride with the positions of the ridden route, otherwise indoor cycling), and CSV
/// for German Excel (semicolon, decimal comma).
/// </summary>
public static class RecordingExport
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>One row of the recording, by column name (columns a recording lacks read as null).</summary>
    private sealed class Row(RecordingFile file, double[] values)
    {
        public double? this[string column] => file.ColumnIndex(column) is var i and >= 0 && i < values.Length ? values[i] : null;
    }

    private const string CsvTimestampTitle = "Zeitstempel";

    /// <summary>Column title in the CSV for Excel: the German names (reading a CSV accepts these and the ids).</summary>
    private static string CsvTitle(string column) =>
        RecordingFile.OldColumns.FirstOrDefault(c => c.Value == column).Key ?? column;

    public static void WriteCsv(RecordingFile file, string path) => WriteComplete(path, temp => WriteCsvTo(file, temp));

    private static void WriteCsvTo(RecordingFile file, string path)
    {
        var text = new StringBuilder();
        text.AppendLine(CsvTimestampTitle + ";" + string.Join(';', file.Columns.Select(CsvTitle)));
        var timeColumn = file.ColumnIndex(RecordingFile.Col.Time);
        foreach (var values in file.Values)
        {
            var time = file.Start.AddSeconds(timeColumn >= 0 ? values[timeColumn] : 0);
            text.AppendLine(time.ToString("yyyy-MM-dd HH:mm:ss", German) + ";" + string.Join(';', values.Select(v => v.ToString(German))));
        }
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Writes into "&lt;path&gt;.tmp" and replaces <paramref name="path"/> only when the file is complete, so a failed
    /// export never leaves a 0-byte file behind or destroys a good one from earlier.
    /// </summary>
    public static void WriteFit(RecordingFile file, string path) => WriteComplete(path, temp => WriteFitTo(file, temp));

    private static void WriteComplete(string path, Action<string> write)
    {
        var temp = path + ".tmp";
        try
        {
            write(temp);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void WriteFitTo(RecordingFile file, string path)
    {
        if (file.Values.Count == 0)
            throw new InvalidDataException("Die Aufzeichnung enthält keine Messwerte.");
        var rows = file.Values.Select(v => new Row(file, v)).ToList();
        var route = file.Target?.Route;
        var virtualRide = route is { Length: > 1 };
        Fit.DateTime At(Row row) => new(file.Start.AddSeconds(row[RecordingFile.Col.Time] ?? 0).ToUniversalTime());

        var start = new Fit.DateTime(file.Start.ToUniversalTime());
        var end = At(rows[^1]);
        var elapsed = (float)(rows[^1][RecordingFile.Col.Time] ?? 0);
        var timer = (float)(rows[^1][RecordingFile.Col.Seconds] ?? elapsed);
        var distance = (float)((rows[^1][RecordingFile.Col.Distance] ?? 0) * 1000);
        var watts = rows.Select(r => r[RecordingFile.Col.Watt] ?? 0).ToList();
        var pulses = rows.Select(r => r[RecordingFile.Col.Pulse] ?? 0).Where(p => p > 0).ToList();
        var cadences = rows.Select(r => r[RecordingFile.Col.Rpm] ?? 0).Where(c => c > 0).ToList();
        var energyKJ = rows[^1][RecordingFile.Col.Energy] ?? watts.Sum() / 1000;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        var encoder = new Fit.Encode(Fit.ProtocolVersion.V20);
        encoder.Open(stream);

        var fileId = new Fit.FileIdMesg();
        fileId.SetType(Fit.File.Activity);
        fileId.SetManufacturer(Fit.Manufacturer.Development);
        fileId.SetProduct(1);
        fileId.SetSerialNumber((uint)(file.Start.Ticks / TimeSpan.TicksPerSecond));
        fileId.SetTimeCreated(start);
        encoder.Write(fileId);

        var device = new Fit.DeviceInfoMesg();
        device.SetDeviceIndex(Fit.DeviceIndex.Creator);
        device.SetManufacturer(Fit.Manufacturer.Development);
        device.SetProductName("ErgoWattControl (Daum ergo_bike 8008 TRS)");
        device.SetTimestamp(start);
        encoder.Write(device);

        encoder.Write(TimerEvent(start, Fit.EventType.Start));
        foreach (var row in rows)
        {
            var record = new Fit.RecordMesg();
            record.SetTimestamp(At(row));
            if (row[RecordingFile.Col.Watt] is { } watt)
                record.SetPower((ushort)Math.Max(0, watt));
            if (row[RecordingFile.Col.Rpm] is { } rpm)
                record.SetCadence((byte)Math.Clamp(rpm, 0, 254));
            if (row[RecordingFile.Col.Pulse] is { } pulse and > 0)
                record.SetHeartRate((byte)Math.Clamp(pulse, 0, 254));
            if (row[RecordingFile.Col.Speed] is { } speed)
                record.SetSpeed((float)(speed / 3.6));
            if (row[RecordingFile.Col.Distance] is { } km)
            {
                record.SetDistance((float)(km * 1000));
                if (virtualRide && PointAt(route!, km * 1000) is { } point)
                {
                    record.SetPositionLat(Semicircles(point.Lat));
                    record.SetPositionLong(Semicircles(point.Lon));
                    record.SetAltitude((float)point.Elevation);
                }
            }
            encoder.Write(record);
        }
        encoder.Write(TimerEvent(end, Fit.EventType.StopAll));

        var lap = new Fit.LapMesg();
        lap.SetMessageIndex(0);
        lap.SetTimestamp(end);
        lap.SetStartTime(start);
        lap.SetTotalElapsedTime(elapsed);
        lap.SetTotalTimerTime(timer);
        lap.SetTotalDistance(distance);
        lap.SetSport(Fit.Sport.Cycling);
        lap.SetSubSport(virtualRide ? Fit.SubSport.VirtualActivity : Fit.SubSport.IndoorCycling);
        lap.SetEvent(Fit.Event.Lap);
        lap.SetEventType(Fit.EventType.Stop);
        encoder.Write(lap);

        var session = new Fit.SessionMesg();
        session.SetMessageIndex(0);
        session.SetTimestamp(end);
        session.SetStartTime(start);
        session.SetTotalElapsedTime(elapsed);
        session.SetTotalTimerTime(timer);
        session.SetTotalDistance(distance);
        session.SetSport(Fit.Sport.Cycling);
        session.SetSubSport(virtualRide ? Fit.SubSport.VirtualActivity : Fit.SubSport.IndoorCycling);
        session.SetAvgPower((ushort)Math.Round(watts.Average()));
        session.SetMaxPower((ushort)watts.Max());
        session.SetTotalWork((uint)Math.Round(energyKJ * 1000));
        // Rule of thumb for cycling: kcal burnt ≈ kJ of mechanical work (about 24 % efficiency).
        session.SetTotalCalories((ushort)Math.Round(energyKJ));
        if (pulses.Count > 0)
        {
            session.SetAvgHeartRate((byte)Math.Round(pulses.Average()));
            session.SetMaxHeartRate((byte)pulses.Max());
        }
        if (cadences.Count > 0)
            session.SetAvgCadence((byte)Math.Round(cadences.Average()));
        session.SetFirstLapIndex(0);
        session.SetNumLaps(1);
        session.SetEvent(Fit.Event.Session);
        session.SetEventType(Fit.EventType.Stop);
        encoder.Write(session);

        var activity = new Fit.ActivityMesg();
        activity.SetTimestamp(end);
        activity.SetTotalTimerTime(timer);
        activity.SetNumSessions(1);
        activity.SetType(Fit.Activity.Manual);
        activity.SetEvent(Fit.Event.Activity);
        activity.SetEventType(Fit.EventType.Stop);
        // Local time as seconds on the FIT clock, so the platforms show the ride at the right hour.
        var offset = TimeZoneInfo.Local.GetUtcOffset(file.Start);
        activity.SetLocalTimestamp((uint)(end.GetTimeStamp() + (long)offset.TotalSeconds));
        encoder.Write(activity);

        encoder.Close();
    }

    private static Fit.EventMesg TimerEvent(Fit.DateTime time, Fit.EventType type)
    {
        var mesg = new Fit.EventMesg();
        mesg.SetTimestamp(time);
        mesg.SetEvent(Fit.Event.Timer);
        mesg.SetEventType(type);
        return mesg;
    }

    /// <summary>Position on the stored route (a point every 10 m: distance, elevation, latitude, longitude).</summary>
    private static (double Elevation, double Lat, double Lon)? PointAt(double[][] route, double meters)
    {
        if (route[0].Length < 4)
            return null;
        var x = Math.Clamp(meters / GpxRoute.Step, 0, route.Length - 1);
        var i = Math.Min((int)x, route.Length - 2);
        var f = x - i;
        double Lerp(int column) => route[i][column] + f * (route[i + 1][column] - route[i][column]);
        return (Lerp(1), Lerp(2), Lerp(3));
    }

    private static int Semicircles(double degrees) => (int)Math.Round(degrees * (Math.Pow(2, 31) / 180));
}
