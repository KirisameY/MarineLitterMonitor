using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MarineLitterMonitor.Server.FileRecording;

public sealed partial class RecordManager(string logPath, string picPath, uint maxPicCount) : IDisposable
{
    private DateOnly _currentDate = DateOnly.MinValue;
    private FileStream? _currentRecordStream;
    private readonly List<AlertRecord> _records = [];
    private readonly Lock _dateLock = new();
    private readonly Lock _logFileLock = new();
    private readonly Lock _picFileLock = new();

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        _currentRecordStream?.Flush();
        _currentRecordStream?.Dispose();
    }

    public void Write(LitterDetectionData data)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        var now = DateTime.Now;

        lock (_dateLock)    // 虽然只有一个写入线程，但读写可能在不同线程，这里锁日期检查部分
        lock (_logFileLock) // 顺便锁一下log文件
        {
            var date = DateOnly.FromDateTime(now);
            bool needRenewStream = false;
            if (_currentDate != date)
            {
                _currentDate = date;
                _records.Clear();
                needRenewStream = true;
            }
            else if (_currentRecordStream is null) needRenewStream = true;

            if (needRenewStream)
            {
                _currentRecordStream?.Flush();
                _currentRecordStream?.Dispose();

                if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
                var filePath = $"{logPath}/log_{_currentDate:yyyy_MM_dd}.record";
                if (File.Exists(filePath))
                {
                    var records = File.ReadLines(filePath).Select(line => JsonSerializer.Deserialize<AlertRecord>(line));
                    _records.AddRange(records);
                }
                _currentRecordStream = File.Open(filePath, FileMode.Append);
            }
        }


        var record = AlertRecord.FromBoundingBoxes(data.BoundingBoxes, now);

        _records.Add(record);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(record);
        _currentRecordStream!.Write(jsonBytes);
        _currentRecordStream!.Write("\n"u8.ToArray());
        _currentRecordStream.Flush();

        lock (_picFileLock) SavePic(picPath, now, data.OutImage, maxPicCount);

        return;

        static void SavePic(string picPath, DateTime time, Image<Rgb24> img, uint maxPic)
        {
            DirectoryInfo picDir = new(picPath);
            if (!picDir.Exists) picDir.Create();
            var savFiles =
                picDir.EnumerateFiles()
                      .Where(file => file.Name.Contains("sav"))
                      .OrderByDescending(sav => sav.Name)
                      .ToList();
            for (int i = savFiles.Count; i > maxPic - 1; i--)
            {
                savFiles[i - 1].Delete();
            }
            var filePath = $"{picPath}/sav_{time:yyyy-MM-dd_HH-mm-ss}.jpg";
            img.SaveAsJpeg(filePath);
        }
    }

    public ImmutableList<AlertRecord>? ReadDay(DateOnly date)
    {
        lock (_dateLock) // 同样锁日期检查&读取当前
        {
            if (date == _currentDate) return _records.ToImmutableList();
        }

        var path = $"{logPath}/log_{date:yyyy_MM_dd}.record";
        if (!File.Exists(path)) return null;
        return File.ReadLines(path)
                   .Select(line => JsonSerializer.Deserialize<AlertRecord>(line))
                   .ToImmutableList();
    }

    public byte[]? ReadPic(DateTime time)
    {
        var filePath = $"{picPath}/sav_{time:yyyy-MM-dd_HH-mm-ss}.jpg";
        lock (_picFileLock) // 图像文件检查和操作，上锁
        {
            return !File.Exists(filePath) ? null : File.ReadAllBytes(filePath);
        }
    }


    [GeneratedRegex(@"^log_(?<t>\d{4}_\d{2}_\d{2})\.record$", RegexOptions.Singleline)]
    private static partial Regex LogNameRegex { get; }

    [GeneratedRegex(@"^sav_(?<t>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.jpg$", RegexOptions.Singleline)]
    private static partial Regex PicNameRegex { get; }

    public IEnumerable<DateOnly> ReadLogs()
    {
        string[] logs;
        lock (_logFileLock)
        {
            DirectoryInfo logDir = new(logPath);
            logs = logDir.EnumerateFiles()
                         .Select(f => LogNameRegex.Match(f.Name))
                         .Where(m => m.Success)
                         .Select(m => m.Groups["t"].Value)
                         .ToArray();
        }

        foreach (var t in logs)
        {
            if (t.Split('_') is not [var y, var m, var d]) throw new Exception("log name date is not 'yyyy-mm-dd', this should not happen");
            yield return new DateOnly(int.Parse(y), int.Parse(m), int.Parse(d));
        }
    }

    public IEnumerable<DateTime> ReadPics()
    {
        string[] picInfos;
        lock (_picFileLock)
        {
            DirectoryInfo picDir = new(picPath);
            picInfos = picDir.EnumerateFiles()
                             .Select(f => PicNameRegex.Match(f.Name))
                             .Where(m => m.Success)
                             .Select(m => m.Groups["t"].Value)
                             .ToArray();
        }
        return picInfos.Select(t =>
        {
            if (t.Split('_') is not [var date, var time] ||
                date.Split('-') is not [var y, var mon, var d] ||
                time.Split('-') is not [var h, var min, var s])
            {
                throw new Exception("log name date is not 'yyyy-MM-dd_HH-mm-ss', this should not happen");
            }
            var dateOnly = new DateOnly(int.Parse(y), int.Parse(mon), int.Parse(d));
            var timeOnly = new TimeOnly(int.Parse(h), int.Parse(min), int.Parse(s));
            return new DateTime(dateOnly, timeOnly);
        });
    }
}