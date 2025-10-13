#include "camera.h"

#include <iostream>
#include <thread>
#include <opencv2/opencv.hpp>

// 用于储存摄像头相关信息
struct CameraContext
{
    cv::VideoCapture cap;
    std::thread grab_thread;
    std::mutex frame_mutex;
    cv::Mat latest_frame;
    std::atomic<bool> stop_thread; // 原子类型，保证线程安全
};

// 使用静态变量，这样摄像头对象在DLL加载期间只初始化一次
static CameraContext* camera_context;

// 线程函数，用于更新当前帧
void grab_loop(CameraContext* context)
{
    cv::Mat local_frame;
    while (!context->stop_thread)
    {
        if (!context->cap.read(local_frame))
        {
            // 读取失败，可能摄像头断开
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            continue;
        }

        if (!local_frame.empty())
        {
            // 锁定互斥锁，安全地更新最新帧
            std::lock_guard lock(context->frame_mutex);
            context->latest_frame = local_frame.clone(); // 使用 clone() 进行深拷贝
        }
    }
}

bool initialize_camera(int* width, int* height)
{
    try
    {
        if (camera_context != nullptr)
        {
            release_camera();
        }

        camera_context = new CameraContext();
        camera_context->stop_thread = false;
        camera_context->cap.open(0); // 0 代表默认摄像头
        if (!camera_context->cap.isOpened())
        {
            std::cerr << "[C++] Error: cap.isOpened() returned false. Failed to open camera." << std::endl;
            delete camera_context;
            return false;
        }

        *width = static_cast<int>(camera_context->cap.get(cv::CAP_PROP_FRAME_WIDTH));
        *height = static_cast<int>(camera_context->cap.get(cv::CAP_PROP_FRAME_HEIGHT));

        // 启动后台抓取线程
        camera_context->grab_thread = std::thread(grab_loop, camera_context);

        return true;
    }
    catch (...)
    {
        return false;
    }
}

bool set_camera_size(const int width, const int height, int* final_width, int* final_height)
{
    const auto set_w = camera_context->cap.set(cv::CAP_PROP_FRAME_WIDTH, width);
    const auto set_h = camera_context->cap.set(cv::CAP_PROP_FRAME_HEIGHT, height);

    bool result = true;
    if (!set_w || !set_h)
    {
        std::cout <<
            "[C++] Warning: cap.set() for resolution returned false. The camera may not support this resolution." <<
            std::endl;
        result = false;
    }

    *final_width = static_cast<int>(camera_context->cap.get(cv::CAP_PROP_FRAME_WIDTH));
    *final_height = static_cast<int>(camera_context->cap.get(cv::CAP_PROP_FRAME_HEIGHT));
    std::cout << "[C++] Info: Current frame width: " << *final_width << ", height: " << *final_height << "." <<
        std::endl;

    return result;
}

int get_webcam_frame(unsigned char* buffer, const int buffer_size,
                     int* out_width, int* out_height, int* out_channels)
{
    if (camera_context == nullptr || !camera_context->cap.isOpened())
    {
        return 0;
    }

    cv::Mat frame;
    {
        // 创建一个临界区
        std::lock_guard lock(camera_context->frame_mutex);
        if (camera_context->latest_frame.empty())
        {
            return 0; // 还没有捕获到任何帧
        }
        // 从共享区域复制一份最新的帧出来，尽快释放锁
        frame = camera_context->latest_frame.clone();
    } // 互斥锁在这里被自动释放

    if (frame.empty())
    {
        return 0;
    }

    {
        // bgr转换为rgb
        cv::Mat rgb;
        cvtColor(frame, rgb, cv::COLOR_BGR2RGB);
        frame = rgb;
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
    if (camera_context == nullptr || !camera_context->cap.isOpened())
    {
        return false;
    }

    cv::Mat frame;
    {
        // 创建一个临界区
        std::lock_guard lock(camera_context->frame_mutex);
        if (camera_context->latest_frame.empty())
        {
            return false; // 还没有捕获到任何帧
        }
        // 从共享区域复制一份最新的帧出来，尽快释放锁
        frame = camera_context->latest_frame.clone();
    } // 互斥锁在这里被自动释放

    if (frame.empty()) return false;

    const int channels = frame.channels();
    if (channels != 3) return false;

    {
        // bgr转换为rgb
        cv::Mat rgb;
        cvtColor(frame, rgb, cv::COLOR_BGR2RGB);
        frame = rgb;
    }

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
                           cv::Scalar(), false, false,CV_32F);

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
    if (camera_context == nullptr) return;

    // 发送停止信号
    camera_context->stop_thread = true;

    // 等待线程退出
    if (camera_context->grab_thread.joinable())
    {
        camera_context->grab_thread.join();
    }

    // 释放其他资源
    camera_context->cap.release();
    delete camera_context;
}
