using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using MarineLitterMonitor;
using MarineLitterMonitor.Server;
using MarineLitterMonitor.Server.ExternImport;
using MarineLitterMonitor.Server.FileRecording;
using MarineLitterMonitor.Server.WebServing;

using MarineLitterMonitorDetection;


await Host.CreateDefaultBuilder(args)
          .ConfigureServices(services => services.AddHostedService<MainApp>())
          .Build()
          .RunAsync();

return 0;


namespace MarineLitterMonitor
{
    internal class MainApp : BackgroundService
    {
        private const int Width = 640;
        private const int Height = 480;
        private const byte OutputPin = 24;

        private const string ModelPath = "models/mlm_s.onnx"; // _n & _s available

        private static readonly ImmutableArray<string> LabelNames =
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

        private const string FontPath = "consola.ttf";

        private const string RecordSaveDirPath = "./record/logs";
        private const string ImageSaveDirPath = "./record/images";
        private const int MaxImageFileCount = 64;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Hello, world!");

            try
            {
                // 初始化IO模块
                using var recordManager = new RecordManager(RecordSaveDirPath, ImageSaveDirPath, MaxImageFileCount);

                // 初始化GPIO
                using var alertGpio = GpioInterface.GetInstance(OutputPin);

                // 初始化检测程序 & 注册检测通知
                var predictor = new YoloV8Predictor(ModelPath, LabelNames, FontPath);
                predictor.ConfidenceThreshold = 0.5f;
                predictor.NmsThreshold        = 0.5f;

                await using var detector = LitterDetector.TryCreateInstance(predictor, Width, Height, 5000);

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
                await using var webServer = await WebServer.StartAsync(recordManager);

                // 开始执行检测
                detector.Start();

                // 阻塞程序直到结束运行
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Running cancelled.");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Unhandled exception found:");
                Console.Error.WriteLine(e.ToString());
            }
            finally
            {
                Console.WriteLine("Program exited.");
            }
        }
    }
}