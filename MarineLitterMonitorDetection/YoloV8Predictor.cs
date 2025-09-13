using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using SixLabors.ImageSharp.Drawing.Processing;

namespace MarineLitterMonitorDetection;

public class YoloV8Predictor : IDisposable
{
    private readonly InferenceSession _session;
    private readonly bool _shouldDisposeSession;
    private readonly string[] _labels;
    private readonly int _modelWidth;
    private readonly int _modelHeight;

    /// <summary>
    /// 获取或设置用于过滤检测结果的置信度阈值。
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>
    /// 获取或设置用于非极大值抑制（NMS）的重叠阈值。
    /// </summary>
    public float NmsThreshold { get; set; } = 0.5f;

    /// <summary>
    /// 通过模型文件路径初始化YOLOv8预测器。
    /// </summary>
    /// <param name="modelPath">ONNX模型文件的路径。</param>
    /// <param name="labels">模型训练时使用的类别标签数组。</param>
    /// <param name="useGpu">是否尝试使用GPU进行推理。</param>
    public YoloV8Predictor(string modelPath, string[] labels, bool useGpu = false)
    {
        var sessionOptions = new SessionOptions();
        if (useGpu)
        {
            // 根据你的环境配置，可能需要 CUDAProviderOptions 或其他
            sessionOptions.AppendExecutionProvider_CUDA();
        }
        _session              = new InferenceSession(modelPath, sessionOptions);
        _shouldDisposeSession = true;
        _labels               = labels;

        // 从模型元数据中获取输入维度
        var inputMetadata = _session.InputMetadata["images"];
        _modelWidth  = inputMetadata.Dimensions[3];
        _modelHeight = inputMetadata.Dimensions[2];
    }

    /// <summary>
    /// 使用一个已存在的InferenceSession实例初始化YOLOv8预测器。
    /// </summary>
    /// <param name="session">要使用的InferenceSession实例。</param>
    /// <param name="labels">模型训练时使用的类别标签数组。</param>
    /// <remarks>当使用此构造函数时，该类不会在Dispose时释放传入的session。</remarks>
    public YoloV8Predictor(InferenceSession session, string[] labels)
    {
        _session              = session;
        _shouldDisposeSession = false;
        _labels               = labels;

        var inputMetadata = _session.InputMetadata["images"];
        _modelWidth  = inputMetadata.Dimensions[3];
        _modelHeight = inputMetadata.Dimensions[2];
    }

    /// <summary>
    /// 对预处理后的图像张量进行目标检测。
    /// </summary>
    /// <param name="imageTensor">符合模型输入的预处理后张量 [1, 3, H, W]。</param>
    /// <returns>一个只读的边界框列表。</returns>
    public IReadOnlyList<BoundingBox> GetBoundingBoxes(DenseTensor<float> imageTensor)
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", imageTensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return Postprocess(output);
    }

    /// <summary>
    /// 异步对预处理后的图像张量进行目标检测。
    /// </summary>
    public Task<IReadOnlyList<BoundingBox>> GetBoundingBoxesAsync(DenseTensor<float> imageTensor)
    {
        // ONNX Runtime的Run方法是同步的CPU密集型操作，用Task.Run移到线程池
        return Task.Run(() => GetBoundingBoxes(imageTensor));
    }

    /// <summary>
    /// 对原始图像进行目标检测，并返回检测结果和绘制了边框的新图像。
    /// </summary>
    /// <param name="originalImage">要进行检测的原始图像。</param>
    /// <returns>一个包含边界框列表和绘制了结果的图像的元组。</returns>
    public (IReadOnlyList<BoundingBox> boxes, Image<Rgba32> annotatedImage) DetectAndDraw(Image<Rgba32> originalImage)
    {
        var (tensor, ratio, padX, padY) = PreprocessImage(originalImage);
        var boxes = GetBoundingBoxes(tensor);

        // 将坐标转换回原始图像尺寸
        var scaledBoxes = new List<BoundingBox>();
        foreach (var box in boxes)
        {
            var x1 = (box.Box.X - padX) / ratio;
            var y1 = (box.Box.Y - padY) / ratio;
            var x2 = (box.Box.Right - padX) / ratio;
            var y2 = (box.Box.Bottom - padY) / ratio;
            scaledBoxes.Add(new BoundingBox(new RectangleF(x1, y1, x2 - x1, y2 - y1), box.Label, box.Confidence));
        }

        var annotatedImage = originalImage.Clone(); // 复制图像以进行绘制
        DrawBoundingBoxes(annotatedImage, scaledBoxes);

        return (scaledBoxes, annotatedImage);
    }

    /// <summary>
    /// 异步对原始图像进行目标检测，并返回检测结果和绘制了边框的新图像。
    /// </summary>
    public Task<(IReadOnlyList<BoundingBox> boxes, Image<Rgba32> annotatedImage)> DetectAndDrawAsync(Image<Rgba32> originalImage)
    {
        return Task.Run(() => DetectAndDraw(originalImage));
    }

    private (DenseTensor<float>, float, float, float) PreprocessImage(Image<Rgba32> image)
    {
        float ratio = Math.Min((float)_modelWidth / image.Width, (float)_modelHeight / image.Height);
        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);
        float padX = (_modelWidth - newWidth) / 2f;
        float padY = (_modelHeight - newHeight) / 2f;

        var tempImage = image.Clone(ctx => ctx
                                       .Resize(new ResizeOptions
                                        {
                                            Size = new Size(newWidth, newHeight),
                                            Mode = ResizeMode.Crop // 使用Crop以保持比例
                                        }));

        var processedImage = new Image<Rgba32>(_modelWidth, _modelHeight);
        processedImage.Mutate(ctx =>
        {
            ctx.Fill(Color.FromRgb(114, 114, 114)); // Letterbox填充色
            ctx.DrawImage(tempImage, new Point((int)padX, (int)padY), 1f);
        });

        var tensor = new DenseTensor<float>(new[] { 1, 3, _modelHeight, _modelWidth });
        processedImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    tensor[0, 0, y, x] = pixelRow[x].R / 255.0f;
                    tensor[0, 1, y, x] = pixelRow[x].G / 255.0f;
                    tensor[0, 2, y, x] = pixelRow[x].B / 255.0f;
                }
            }
        });

        return (tensor, ratio, padX, padY);
    }

    private IReadOnlyList<BoundingBox> Postprocess(Tensor<float> output)
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

        return NonMaxSuppression(detections);
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

    private static void DrawBoundingBoxes(Image<Rgba32> image, IEnumerable<BoundingBox> boxes)
    {
        // 字体需要你自己提供或者系统安装，这里仅为示例
        // Font font = SystemFonts.CreateFont("Arial", 12);

        foreach (var box in boxes)
        {
            image.Mutate(ctx =>
            {
                ctx.Draw(Color.Red, 2, box.Box);
                // 暂时不画文字，因为字体加载需要额外处理
                // ctx.DrawText($"{box.Label} {box.Confidence:P2}", font, Color.White, Brushes.Black, new PointF(box.Box.Left, box.Box.Top - 15));
            });
        }
    }

    // 辅助方法：转置张量
    private static DenseTensor<float> Transpose(Tensor<float> tensor, int[] permutation)
    {
        var newShape = permutation.Select(i => tensor.Dimensions[i]).ToArray();
        var newTensor = new DenseTensor<float>(newShape);
        var originalStrides = new int[tensor.Rank];
        originalStrides[tensor.Rank - 1] = 1;
        for (int i = tensor.Rank - 2; i >= 0; i--)
        {
            originalStrides[i] = originalStrides[i + 1] * tensor.Dimensions[i + 1];
        }

        for (int i = 0; i < tensor.Length; i++)
        {
            int originalIndex = i;
            int newIndex = 0;
            for (int j = tensor.Rank - 1; j >= 0; j--)
            {
                int originalCoord = originalIndex / originalStrides[j];
                originalIndex %= originalStrides[j];

                int permutedDim = Array.IndexOf(permutation, j);
                newIndex += originalCoord * (newTensor.Strides[permutedDim]);
            }
            newTensor.SetValue(newIndex, tensor.GetValue(i));
        }
        return newTensor;
    }

    public void Dispose()
    {
        if (_shouldDisposeSession)
        {
            _session.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}