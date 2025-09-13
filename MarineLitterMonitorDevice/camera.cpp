#include "camera.h"

#include <iostream>
#include <opencv2/opencv.hpp>


// 使用静态变量，这样摄像头对象在DLL加载期间只初始化一次
static cv::VideoCapture cap;

bool InitializeCamera(int* width, int* height)
{
    if (cap.isOpened())
    {
        cap.release();
    }
    // 0 代表默认摄像头
    cap.open(0, cv::CAP_V4L2);
    if (!cap.isOpened())
    {
        std::cerr << "[C++] Error: cap.isOpened() returned false. Failed to open camera." << std::endl;
        return false;
    }

    *width = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_WIDTH));
    *height = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_HEIGHT));

    return true;
}

bool SetCameraSize(const int width, const int height, int* finalWidth, int* finalHeight)
{
    const auto set_w = cap.set(cv::CAP_PROP_FRAME_WIDTH, width);
    const auto set_h = cap.set(cv::CAP_PROP_FRAME_HEIGHT, height);

    bool result = true;
    if (!set_w || !set_h)
    {
        std::cout <<
            "[C++] Warning: cap.set() for resolution returned false. The camera may not support this resolution." <<
            std::endl;
        result = false;
    }

    *finalWidth = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_WIDTH));
    *finalHeight = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_HEIGHT));
    std::cout << "[C++] Info: Current frame width: " << finalWidth << ", height: " << finalHeight << "." << std::endl;

    return result;
}

int GetWebcamFrame(unsigned char* buffer, const int bufferSize,
                   int* outWidth, int* outHeight, int* outChannels,
                   const bool toRgb)
{
    if (!cap.isOpened())
    {
        return 0;
    }

    cv::Mat frame;
    cap.read(frame);

    if (frame.empty())
    {
        return 0;
    }

    // OpenCV默认读取的格式是BGR，而大多数环境需要RGB。
    // 我们在这里进行转换。
    if (toRgb)
    {
        cv::Mat rgbFrame;
        cvtColor(frame, rgbFrame, cv::COLOR_BGR2RGB);
        frame = rgbFrame;
    }

    const int channels = frame.channels();
    const int requiredSize = frame.rows * frame.cols * channels;

    // 检查C#提供的缓冲区大小是否足够
    if (bufferSize < requiredSize)
    {
        return 0; // 缓冲区太小
    }

    // 将图像数据复制到C#传入的缓冲区
    memcpy(buffer, frame.data, requiredSize);

    // 通过指针返回图像的实际尺寸和通道数
    *outWidth = frame.cols;
    *outHeight = frame.rows;
    *outChannels = channels;

    return requiredSize;
}

void ReleaseCamera()
{
    if (cap.isOpened())
    {
        cap.release();
    }
}
