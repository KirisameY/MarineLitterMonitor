#include "camera.h"

#include <iostream>
#include <opencv2/opencv.hpp>


// 使用静态变量，这样摄像头对象在DLL加载期间只初始化一次
static cv::VideoCapture cap;

bool initialize_camera(int* width, int* height)
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

bool set_camera_size(const int width, const int height, int* final_width, int* final_height)
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

    *final_width = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_WIDTH));
    *final_height = static_cast<int>(cap.get(cv::CAP_PROP_FRAME_HEIGHT));
    std::cout << "[C++] Info: Current frame width: " << *final_width << ", height: " << *final_height << "." <<
        std::endl;

    return result;
}

int get_webcam_frame(unsigned char* buffer, const int buffer_size,
                     int* out_width, int* out_height, int* out_channels)
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

    const int channels = frame.channels();
    const int requiredSize = frame.rows * frame.cols * channels;

    // 检查C#提供的缓冲区大小是否足够
    if (buffer_size < requiredSize)
    {
        return 0; // 缓冲区太小
    }

    // 将图像数据复制到C#传入的缓冲区
    memcpy(buffer, frame.data, requiredSize);

    // 通过指针返回图像的实际尺寸和通道数
    *out_width = frame.cols;
    *out_height = frame.rows;
    *out_channels = channels;

    return requiredSize;
}


bool get_webcam_frame_and_normalized(unsigned char* buffer, const int buffer_size, int* out_size,
                                     unsigned char* n_buffer, const int n_buffer_size, int* n_out_size,
                                     int* out_width, int* out_height)
{
    if (!cap.isOpened())
    {
        return false;
    }

    cv::Mat frame;
    cap.read(frame);

    if (frame.empty()) return false;

    const int channels = frame.channels();
    if (channels != 3) return false;

    const int requiredSize = frame.rows * frame.cols * channels;
    const int n_requiredSize = requiredSize * 4;

    // 检查外部提供的缓冲区大小是否足够
    if (buffer_size < requiredSize || n_buffer_size < n_requiredSize)
    {
        return false; // 缓冲区太小
    }

    // 转换格式
    cv::Mat blob;
    cv::dnn::blobFromImage(frame, blob, 1.0 / 255.0, cv::Size(frame.cols, frame.rows),
                           cv::Scalar(), true, false,CV_32F);

    // 将图像数据复制到C#传入的缓冲区
    memcpy(buffer, frame.data, requiredSize);
    memcpy(n_buffer, blob.data, n_requiredSize);

    // 通过指针返回图像的实际尺寸和通道数
    *out_width = frame.cols;
    *out_height = frame.rows;

    *out_size = requiredSize;
    *n_out_size = n_requiredSize;

    return true;
}


void release_camera()
{
    if (cap.isOpened())
    {
        cap.release();
    }
}
