using SixLabors.ImageSharp;

namespace MarineLitterMonitorDetection;

/// <summary>
/// 表示一个检测到的边界框。
/// </summary>
/// <param name="Box">边界框的位置和大小。</param>
/// <param name="Label">识别出的物体类别名称。</param>
/// <param name="Confidence">该识别结果的置信度 (0-1)。</param>
public readonly record struct BoundingBox(RectangleF Box, string Label, float Confidence);