using System.Collections.Immutable;

using MarineLitterMonitorDetection;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MarineLitterMonitor.Server;

public readonly record struct LitterDetectionData(Image<Rgb24> OutImage, ImmutableArray<BoundingBox> BoundingBoxes);