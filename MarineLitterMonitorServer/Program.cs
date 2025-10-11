using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

using MarineLitterMonitor.Server;
using MarineLitterMonitor.Server.ExternImport;

using MarineLitterMonitorDetection;

using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

void BeforeExit()
{
    // Console.WriteLine("Press any key to continue...");
    // Console.ReadKey();
    Console.WriteLine("Program exited.");
}

const int width = 640;
const int height = 480;
const byte outputPin = 24;

const string modelPath = "models/mlm_s.onnx"; // _n & _s available
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

const string fontPath = "consola.ttf";

const string saveDirPath = "./sav";
const int maxSavFileCount = 64;


CancellationTokenSource cancellationTokenSource = new();
TaskCompletionSource shutdownSource = new();
AssemblyLoadContext.Default.Unloading += _ =>
{
    Console.WriteLine("SIGTERM received.");
    if (!cancellationTokenSource.IsCancellationRequested)
        cancellationTokenSource.Cancel();
    // 阻塞 Unloading 事件线程，直到主逻辑清理完毕
    shutdownSource.Task.Wait();
};


Console.WriteLine("Hello, world!");

try
{
    var predictor = new YoloV8Predictor(modelPath, labelNames, fontPath);
    predictor.ConfidenceThreshold = 0.5f;
    predictor.NmsThreshold        = 0.5f;

    using var detector = LitterDetector.TryCreateInstance(predictor, width, height, 5000);
    using var alertGpio = GpioInterface.GetInstance(outputPin);

    if (detector is null) throw new Exception("Detector initialize failed.");

    detector.LitterDetected += (sender, data) =>
    {
        DirectoryInfo savDir = new(saveDirPath);
        if (!savDir.Exists) savDir.Create();
        var savFiles =
            savDir.EnumerateFiles()
                  .Where(file => file.Name.Contains("sav"))
                  .OrderByDescending(sav => sav.Name)
                  .ToList();
        for (int i = savFiles.Count; i > maxSavFileCount - 1; i--)
        {
            savFiles[i - 1].Delete();
        }
        var filePath = $"{saveDirPath}/sav_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.jpg";

        data.OutImage.SaveAsJpeg(filePath);

        Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")] async () =>
        {
            alertGpio.Write(true);
            await Task.Delay(200);
            alertGpio.Write(false);
        }).Wait();
    };
}
catch (OperationCanceledException)
{
    Console.WriteLine("Running cancelled by user.");
}
catch (Exception e)
{
    Console.Error.WriteLine("Unhandled exception found:");
    Console.Error.WriteLine(e.ToString());
}
finally
{
    BeforeExit();
    shutdownSource.SetResult();
}
return 0;