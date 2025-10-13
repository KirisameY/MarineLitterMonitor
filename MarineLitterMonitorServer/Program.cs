using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

using MarineLitterMonitor.Server;
using MarineLitterMonitor.Server.ExternImport;
using MarineLitterMonitor.Server.FileRecording;
using MarineLitterMonitor.Server.WebServing;

using MarineLitterMonitorDetection;

using SixLabors.ImageSharp;

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

const string recordSaveDirPath = "./record/logs";
const string imageSaveDirPath = "./record/images";
const int maxImageFileCount = 64;


// CancellationTokenSource cancellationTokenSource = new();
TaskCompletionSource cancellationRequestTaskSource = new();
TaskCompletionSource shutdownSource = new();
AssemblyLoadContext.Default.Unloading += _ =>
{
    Console.WriteLine("SIGTERM received.");
    // if (!cancellationTokenSource.IsCancellationRequested)
    //     cancellationTokenSource.Cancel();
    cancellationRequestTaskSource.TrySetCanceled();
    // 阻塞 Unloading 事件线程，直到主逻辑清理完毕
    shutdownSource.Task.Wait();
};
Console.CancelKeyPress += (_, eventArgs) =>
{
    Console.WriteLine("Cancel request received.");
    cancellationRequestTaskSource.TrySetCanceled();
    eventArgs.Cancel = true;
};


Console.WriteLine("Hello, world!");

try
{
    // 初始化IO模块
    using var recordManager = new RecordManager(recordSaveDirPath, imageSaveDirPath, maxImageFileCount);

    // 初始化GPIO
    using var alertGpio = GpioInterface.GetInstance(outputPin);

    // 初始化检测程序 & 注册检测通知
    var predictor = new YoloV8Predictor(modelPath, labelNames, fontPath);
    predictor.ConfidenceThreshold = 0.5f;
    predictor.NmsThreshold        = 0.5f;

    await using var detector = LitterDetector.TryCreateInstance(predictor, width, height, 5000);

    if (detector is null) throw new Exception("Detector initialize failed.");

    detector.LitterDetected += (_, data) =>
    {
        // ReSharper disable once AccessToDisposedClosure
        var tSav = Task.Run(() => recordManager.Write(data));
        var tAlt = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")] async () =>
        {
            alertGpio.Write(true);
            await Task.Delay(200);
            alertGpio.Write(false);
        });

        Task.WaitAll(tSav, tAlt);
    };

    // 初始化网络服务
    await using var webServer = await WebServer.StartAsync();

    // 阻塞程序直到结束运行
    await cancellationRequestTaskSource.Task;
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