#ifndef MARINELITTERMONITORDEVICE_LIBRARY_H
#define MARINELITTERMONITORDEVICE_LIBRARY_H

#include <cstdint>

extern "C" {
// 初始化摄像头，返回true表示成功
bool initialize_camera(int* width, int* height);

// 设置摄像头的图像尺寸，返回true表示设置成功
bool set_camera_size(int width, int height, int* final_width, int* final_height);

// 获取一帧图像数据，填充到调用者提供的buffer中
// 返回值为实际写入的字节数，如果失败则返回0
int get_webcam_frame(unsigned char* buffer, int buffer_size,
                   int* out_width, int* out_height, int* out_channels,
                   bool to_rgb);

// 释放摄像头资源
void release_camera();
}

#endif //MARINELITTERMONITORDEVICE_LIBRARY_H
