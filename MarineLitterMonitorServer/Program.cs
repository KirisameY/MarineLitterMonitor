using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using MarineLitterMonitor.Server.ExternImport;

using MarineLitterMonitorDetection;

using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

void BeforeExit()
{
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
    Console.WriteLine();
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
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
    Console.WriteLine("Cancel requested.");
};


Console.WriteLine("Hello, world!");

#region cameraInit

CameraInterface cameraInit;

try
{
    cameraInit = CameraInterface.GetInstance();
    cameraInit.SetSize(width, height);
    if (cameraInit.Size != (width, height))
    {
        Console.WriteLine($"Error: camera size is {cameraInit.Size}. (expected: {(width, height)})");
        BeforeExit();
        return 0;
    }
}
catch (CameraInterface.CameraInitializationException)
{
    Console.WriteLine("Error: camera initialize failed.");
    BeforeExit();
    return 0;
}

Console.WriteLine("Camera initialized.");

#endregion


try
{
    using var camera = cameraInit;
    using var alertGpio = GpioInterface.GetInstance(outputPin);

    byte[] buffer = new byte[width * height * 3];
    float[] nBuffer = new float[width * height * 3];

    using var predictor = new YoloV8Predictor(modelPath, labelNames, fontPath);
    predictor.ConfidenceThreshold = 0.5f;
    predictor.NmsThreshold        = 0.5f;

    List<Task> frameTasks = [];
    while (!cancellationTokenSource.Token.IsCancellationRequested)
    {
        frameTasks.Clear();
        frameTasks.Add(Task.Delay(5000, cancellationTokenSource.Token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            if (t.IsFaulted) throw t.Exception;
        }));

        var stopWatch = Stopwatch.StartNew();

        var (bytes, nBytes) = camera.GetFrameAndNormalized(buffer, nBuffer);

        using var img = Image.LoadPixelData<Rgb24>(buffer, width, height);
        var inputTensor = new DenseTensor<float>(nBuffer.AsMemory(), [1, 3, height, width]);
        var (result, outImg) = await predictor.DetectAndDrawAsync(img, inputTensor);

        if (result.Length > 0)
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

            var savTask = outImg.SaveAsJpegAsync(filePath);
            frameTasks.Add(savTask);

            var alertTask = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")] async () =>
            {
                alertGpio.Write(true);
                await Task.Delay(200);
                alertGpio.Write(false);
            });
            frameTasks.Add(alertTask);
        }

        await Task.WhenAll(frameTasks);
        stopWatch.Stop();
        Console.WriteLine($"loop used {stopWatch.ElapsedMilliseconds} ms.");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Running cancelled by user.");
}
catch (Exception e)
{
    Console.WriteLine("Unhandled exception found:");
    Console.WriteLine(e.ToString());
}
finally
{
    BeforeExit();
}
return 0;