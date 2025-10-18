using System.Collections.Immutable;

using MarineLitterMonitorDetection;

namespace MarineLitterMonitor.Server.FileRecording;

public readonly record struct AlertRecord(TimeOnly Time, ImmutableArray<AlertRecordEntry> Entries)
{
    public static AlertRecord FromBoundingBoxes(IEnumerable<BoundingBox> boxes, DateTime time) =>
        FromBoundingBoxes(boxes, TimeOnly.FromDateTime(time));

    public static AlertRecord FromBoundingBoxes(IEnumerable<BoundingBox> boxes, TimeOnly time)
    {
        var entries = boxes.Select(b => AlertRecordEntry.FromBoundingBox(b)).ToImmutableArray();
        return new(time, entries);
    }
}

public readonly record struct AlertRecordEntry(string Label, float Confidence)
{
    public static AlertRecordEntry FromBoundingBox(BoundingBox box) => new(box.Label, box.Confidence);
}