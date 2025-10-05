using System.Collections.Immutable;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace MarineLitterMonitorDetection;

public class YoloV8Predictor(InferenceSession session, IEnumerable<string> labels, Font? font = null)
    : IDisposable
{
    #region Initialize & Dispose

    public YoloV8Predictor(string modelPath, IEnumerable<string> labels, string? fontPath) :
        this(CreateSession(modelPath), labels, CreateFont(fontPath))
    {
        _shouldDisposeSession = true;
    }

    private static InferenceSession CreateSession(string modelPath) => new(modelPath, new SessionOptions());

    private static Font? CreateFont(string? fontPath)
    {
        if (fontPath is null) return null;
        if (!File.Exists(fontPath)) return null;

        var fontCollection = new FontCollection();
        var fontFamily = fontCollection.Add(fontPath);
        return fontFamily.CreateFont(14, FontStyle.Regular);
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        if (_shouldDisposeSession)
        {
            session.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    #endregion


    #region Public Api

    private readonly ImmutableArray<string> _labels = labels.ToImmutableArray();
    private readonly bool _shouldDisposeSession = false;

    /// <summary>
    /// 获取或设置用于过滤检测结果的置信度阈值。
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>
    /// 获取或设置用于非极大值抑制（NMS）的重叠阈值。
    /// </summary>
    public float NmsThreshold { get; set; } = 0.5f;

    /// <summary>
    /// 对预处理后的图像张量进行目标检测。
    /// </summary>
    /// <param name="imageTensor">符合模型输入的预处理后张量 [1, 3, H, W]。</param>
    /// <returns>一个只读的边界框列表。</returns>
    public ImmutableArray<BoundingBox> GetBoundingBoxes(DenseTensor<float> imageTensor)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", imageTensor)
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return Postprocess(output);
    }

    /// <summary>
    /// 异步对预处理后的图像张量进行目标检测。
    /// </summary>
    public Task<ImmutableArray<BoundingBox>> GetBoundingBoxesAsync(DenseTensor<float> imageTensor)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        // ONNX Runtime的Run方法是同步的CPU密集型操作，用Task.Run移到线程池
        return Task.Run(() => GetBoundingBoxes(imageTensor));
    }

    /// <summary>
    /// 对原始图像进行目标检测，并返回检测结果和绘制了边框的新图像。
    /// </summary>
    /// <param name="originalImage">要进行检测的原始图像。</param>
    /// <param name="imageTensor">符合模型输入的预处理后张量 [1, 3, H, W]。</param>
    /// <returns>一个包含边界框列表和绘制了结果的图像的元组。</returns>
    public (ImmutableArray<BoundingBox> boxes, Image<Rgb24> annotatedImage) DetectAndDraw(Image<Rgb24> originalImage, DenseTensor<float> imageTensor)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        var boxes = GetBoundingBoxes(imageTensor);

        var annotatedImage = originalImage.Clone(); // 复制图像以进行绘制
        DrawBoundingBoxes(annotatedImage, boxes, font);

        return (boxes.ToImmutableArray(), annotatedImage);
    }

    /// <summary>
    /// 异步对原始图像进行目标检测，并返回检测结果和绘制了边框的新图像。
    /// </summary>
    public Task<(ImmutableArray<BoundingBox> boxes, Image<Rgb24> annotatedImage)> DetectAndDrawAsync(Image<Rgb24> originalImage, DenseTensor<float> imageTensor)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        return Task.Run(() => DetectAndDraw(originalImage, imageTensor));
    }

    #endregion


    #region Support methods

    private ImmutableArray<BoundingBox> Postprocess(Tensor<float> output)
    {
        // YOLOv8的输出是 [1, 84, 8400]。我们需要将其转置为 [1, 8400, 84] 以方便处理
        var transposedOutput = Transpose(output, new[] { 0, 2, 1 });
        var detections = new List<BoundingBox>();

        for (int i = 0; i < transposedOutput.Dimensions[1]; i++)
        {
            var classScores = new List<float>();
            for (int j = 4; j < transposedOutput.Dimensions[2]; j++)
            {
                classScores.Add(transposedOutput[0, i, j]);
            }

            var maxScore = classScores.Max();
            if (maxScore < ConfidenceThreshold) continue;

            var maxIndex = classScores.IndexOf(maxScore);

            float xCenter = transposedOutput[0, i, 0];
            float yCenter = transposedOutput[0, i, 1];
            float width = transposedOutput[0, i, 2];
            float height = transposedOutput[0, i, 3];

            detections.Add(new BoundingBox(
                               new RectangleF(xCenter - width / 2, yCenter - height / 2, width, height),
                               _labels[maxIndex],
                               maxScore
                           ));
        }

        return NonMaxSuppression(detections).ToImmutableArray();
    }

    private List<BoundingBox> NonMaxSuppression(List<BoundingBox> boxes)
    {
        var finalBoxes = new List<BoundingBox>();
        boxes = boxes.OrderByDescending(b => b.Confidence).ToList();

        while (boxes.Count > 0)
        {
            var currentBox = boxes[0];
            finalBoxes.Add(currentBox);
            boxes.RemoveAt(0);

            boxes = boxes.Where(b => CalculateIoU(currentBox, b) < NmsThreshold).ToList();
        }
        return finalBoxes;
    }

    private static float CalculateIoU(BoundingBox boxA, BoundingBox boxB)
    {
        var intersection = RectangleF.Intersect(boxA.Box, boxB.Box);
        if (intersection.IsEmpty) return 0;

        var union = (boxA.Box.Width * boxA.Box.Height) + (boxB.Box.Width * boxB.Box.Height) - intersection.Width * intersection.Height;
        return (intersection.Width * intersection.Height) / union;
    }

    private static void DrawBoundingBoxes(Image<Rgb24> image, IEnumerable<BoundingBox> boxes, Font? font)
    {
        // 字体需要你自己提供或者系统安装，这里仅为示例
        // Font font = SystemFonts.CreateFont("Arial", 12);

        foreach (var box in boxes)
        {
            image.Mutate(ctx =>
            {
                ctx.Draw(Color.Red, 2, box.Box);

                // 只有在提供了字体时才绘制文本
                if (font is not null)
                {
                    string label = $"{box.Label} {box.Confidence:P2}";

                    // 使用RichTextOptions来控制文本渲染
                    var textOptions = new RichTextOptions(font)
                    {
                        Origin = new PointF(0, 0) // 从原点开始测量
                    };

                    // 测量文本尺寸，以便绘制背景
                    FontRectangle measuredSize = TextMeasurer.MeasureBounds(label, textOptions);

                    // 计算文本框的位置（通常在主框的左上角上方）
                    float textX = box.Box.Left;
                    float textY = box.Box.Top - measuredSize.Height - 4; // 向上偏移并留出一点边距

                    // 如果文本框会超出图像顶部，则将其移动到主框内部
                    if (textY < 0)
                    {
                        textY = box.Box.Top + 2;
                    }

                    // 创建文本背景框，并增加一点内边距
                    var backgroundRect = new RectangleF(
                        textX,
                        textY,
                        measuredSize.Width + 8,
                        measuredSize.Height + 4
                    );

                    var textLocation = new PointF(textX + 4, textY + 2);

                    // 绘制文本背景
                    ctx.Fill(Brushes.Solid(Color.Red), backgroundRect);

                    // 绘制文本
                    ctx.DrawText(label, font, Color.White, textLocation);
                }
            });
        }
    }

    // 辅助方法：转置张量
    private static DenseTensor<float> Transpose(Tensor<float> tensor, int[] permutation)
    {
        // 1. 计算新张量的维度
        var newShape = permutation.Select(i => tensor.Dimensions[i]).ToArray();
        var newTensor = new DenseTensor<float>(newShape);

        // 2. 计算源张量和目标张量的步长（Strides）
        // 步长是指在某个维度上移动一个单位，在线性存储中需要跳过的元素数量
        var originalStrides = new int[tensor.Rank];
        originalStrides[tensor.Rank - 1] = 1;
        for (int i = tensor.Rank - 2; i >= 0; i--)
        {
            originalStrides[i] = originalStrides[i + 1] * tensor.Dimensions[i + 1];
        }

        var newStrides = new int[newTensor.Rank];
        newStrides[newTensor.Rank - 1] = 1;
        for (int i = newTensor.Rank - 2; i >= 0; i--)
        {
            newStrides[i] = newStrides[i + 1] * newTensor.Dimensions[i + 1];
        }

        // 3. 遍历源张量的每一个元素
        for (int i = 0; i < tensor.Length; i++)
        {
            // 3a. 从线性索引 i 计算出源张量的多维坐标
            var originalCoords = new int[tensor.Rank];
            int tempIndex = i;
            for (int j = 0; j < tensor.Rank; j++)
            {
                originalCoords[j] =  tempIndex / originalStrides[j];
                tempIndex         %= originalStrides[j];
            }

            // 3b. 根据 permutation 计算目标张量中的新多维坐标
            var newCoords = new int[newTensor.Rank];
            for (int j = 0; j < newTensor.Rank; j++)
            {
                // 新坐标的第 j 维，其值来自于老坐标的第 permutation[j] 维
                newCoords[j] = originalCoords[permutation[j]];
            }

            // 3c. 从新的多维坐标计算出目标张量的线性索引
            int newIndex = 0;
            for (int j = 0; j < newTensor.Rank; j++)
            {
                newIndex += newCoords[j] * newStrides[j];
            }

            // 3d. 将值从源张量复制到目标张量
            newTensor.SetValue(newIndex, tensor.GetValue(i));
        }

        return newTensor;
    }

    #endregion
}