// See https://aka.ms/new-console-template for more information

using System.Collections.Immutable;

using MarineLitterMonitorDetection;

using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const string modelPath = "models/mlm_s.onnx";
ImmutableArray<string> labelNames =
[
    "Aluminium foil", "Battery", "Aluminium blister pack", "Carded blister pack",
    "Other plastic bottle", "Clear plastic bottle", "Glass bottle", "Plastic bottle cap",
    "Metal bottle cap", "Broken glass", "Food Can", "Aerosol",
    "Drink can", "Toilet tube", "Other carton", "Egg carton",
    "Drink carton", "Corrugated carton", "Meal carton", "Pizza box",
    "Paper cup", "Disposable plastic cup", "Foam cup", "Glass cup",
    "Other plastic cup", "Food waste", "Glass jar", "Plastic lid",
    "Metal lid", "Other plastic", "Magazine paper", "Tissues",
    "Wrapping paper", "Normal paper", "Paper bag", "Plastified paper bag",
    "Plastic film", "Six pack rings", "Garbage bag", "Other plastic wrapper",
    "Single-use carrier bag", "Polypropylene bag", "Crisp packet", "Spread tub",
    "Tupperware", "Disposable food container", "Foam food container", "Other plastic container",
    "Plastic glooves", "Plastic utensils", "Pop tab", "Rope & strings",
    "Scrap metal", "Shoe", "Squeezable tube", "Plastic straw",
    "Paper straw", "Styrofoam piece", "Unlabeled litter", "Cigarette",
];

ImmutableArray<string> picturePaths = Enumerable.Range(0, 5).Select(i => $"{i}").ToImmutableArray();

const string fontPath = "consola.ttf";

Console.WriteLine("Hello, World!");
var modelFileInfo = new FileInfo(modelPath);

using var predictor = new YoloV8Predictor(modelPath, labelNames, fontPath);
predictor.ConfidenceThreshold = 0.5f;
predictor.NmsThreshold        = 0.5f;

foreach (var picturePath in picturePaths)
{
    var fullpath = $"./test/{picturePath}.png";
    using var img = Image.Load(fullpath);
    using var imgRgb = img.CloneAs<Rgb24>();

    var (_, imgOut) = predictor.DetectAndDraw(imgRgb, PreprocessImage(imgRgb));
    imgOut.Save($"./test/out_{picturePath}.png");
}

return;

DenseTensor<float> PreprocessImage(Image<Rgb24> image)
{
    var processedImage = image.Clone();

    var tensor = new DenseTensor<float>(new[] { 1, 3, image.Height, image.Width });
    processedImage.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < accessor.Height; y++)
        {
            Span<Rgb24> pixelRow = accessor.GetRowSpan(y);
            for (int x = 0; x < accessor.Width; x++)
            {
                tensor[0, 0, y, x] = pixelRow[x].R / 255.0f;
                tensor[0, 1, y, x] = pixelRow[x].G / 255.0f;
                tensor[0, 2, y, x] = pixelRow[x].B / 255.0f;
            }
        }
    });

    return tensor;
}