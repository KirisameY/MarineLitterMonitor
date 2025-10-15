using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using MarineLitterMonitor.Server.ExternImport;

using MarineLitterMonitorDetection;

using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MarineLitterMonitor.Server;

internal class LitterDetector : IAsyncDisposable
{
    public event EventHandler<LitterDetectionData>? LitterDetected;

    private readonly CancellationTokenSource _cancelTokenSource = new();
    private readonly TaskCompletionSource _startSource = new();
    private readonly Task _task;

    private LitterDetector(CameraInterface cameraIn, YoloV8Predictor predictorIn, ushort frameWidth, ushort frameHeight, uint frameTimeMs)
    {
        _task = Task.Run((Func<Task>)(async Task () =>
        {
            using var camera = cameraIn;
            using var predictor = predictorIn;

            List<Task> frameTasks = [];
            byte[] buffer = new byte[frameWidth * frameHeight * 3];
            float[] nBuffer = new float[frameWidth * frameHeight * 3];

            await _startSource.Task;
            Console.WriteLine("Detection loop started!");

            var cancelToken = _cancelTokenSource.Token;
            while (!cancelToken.IsCancellationRequested)
            {
                frameTasks.Clear();
                frameTasks.Add(Task.Delay((int)frameTimeMs, cancelToken).ContinueWith(t =>
                {
                    if (t.IsCanceled) return;
                    if (t.IsFaulted) throw t.Exception;
                }));

                var stopWatch = Stopwatch.StartNew();

                _ = camera.GetFrameAndNormalized(buffer, nBuffer);

                using var img = Image.LoadPixelData<Rgb24>(buffer, frameWidth, frameHeight);
                var inputTensor = new DenseTensor<float>(nBuffer.AsMemory(), [1, 3, frameHeight, frameWidth]);
                var (result, outImg) = await predictor.DetectAndDrawAsync(img, inputTensor);

                if (result.Length > 0)
                {
                    var notificationTask = Task.Run(() => LitterDetected?.Invoke(this, new LitterDetectionData(outImg, result)));
                    frameTasks.Add(notificationTask);
                }

                await Task.WhenAll(frameTasks);
                stopWatch.Stop();
                Console.WriteLine($"loop used {stopWatch.ElapsedMilliseconds} ms.");
            }
        }));

        _task.ContinueWith(t =>
        {
            Console.Error.WriteLine("Unhandled exception thrown from LitterDetector task: ");
            Console.Error.WriteLine(t.Exception!.Flatten());
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public static LitterDetector? TryCreateInstance(YoloV8Predictor predictor, ushort frameWidth, ushort frameHeight, uint frameTimeMs)
    {
        CameraInterface camera;

        try
        {
            camera = CameraInterface.GetInstance();
            camera.SetSize(frameWidth, frameHeight);
            if (camera.Size != (frameWidth, frameHeight))
            {
                Console.WriteLine($"Error: camera size is {camera.Size}. (expected: {(frameWidth, frameHeight)})");
                return null;
            }
        }
        catch (CameraInterface.CameraInitializationException)
        {
            Console.WriteLine("Error: camera initialize failed.");
            return null;
        }

        Console.WriteLine("Camera initialized.");

        return new(camera, predictor, frameWidth, frameHeight, frameTimeMs);
    }

    public void Start()
    {
        _startSource.TrySetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _cancelTokenSource.CancelAsync();
        await _task;
    }
}