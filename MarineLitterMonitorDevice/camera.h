#ifndef MARINELITTERMONITORDEVICE_LIBRARY_H
#define MARINELITTERMONITORDEVICE_LIBRARY_H

#include <cstdint>

extern "C" {
// 初始化摄像头，返回true表示成功
bool InitializeCamera(int width, int height);

// 获取一帧图像数据，填充到调用者提供的buffer中
// 返回值为实际写入的字节数，如果失败则返回0
int GetWebcamFrame(unsigned char* buffer, int bufferSize,
                   int* outWidth, int* outHeight, int* outChannels,
                   bool toRgb);

// 释放摄像头资源
void ReleaseCamera();
}

#endif //MARINELITTERMONITORDEVICE_LIBRARY_H
