using System.Collections.Immutable;

using MarineLitterMonitor.Server.ExternImport;

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

const string modelPath = "models/mlm_n.onnx"; // _n & _m available
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

    byte[] buffer = new byte[width * height * 3];
    byte[] nBuffer = new byte[width * height * 3 * 4];
    // this takes about 4ms
    var (bytes, nBytes) = camera.GetFrameAndNormalized(buffer, nBuffer);
    Console.WriteLine($"read bytes: {bytes}");
    Console.WriteLine($"read nBytes: {nBytes}");

    var img = Image.LoadPixelData<Bgr24>(buffer, width, height);
    img.Save("./save.png");

    // using (var gpioOut = GpioInterface.GetInstance(outputPin))
    // {
    //     gpioOut.Write(true);
    //     Console.WriteLine("Any key to stop");
    //     Console.ReadKey();
    //     gpioOut.Write(false);
    // }
}
finally
{
    BeforeExit();
}
return 0;