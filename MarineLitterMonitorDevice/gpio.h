#ifndef MARINELITTERMONITORDEVICE_GPIO_H
#define MARINELITTERMONITORDEVICE_GPIO_H

#include <cstdint>

extern "C" {
// 导出一个GPIO引脚，使其在 /sys/class/gpio 中可见，返回true为成功。
bool gpio_export(uint8_t pin);

// 释放一个导出的GPIO引脚。
bool gpio_unexport(uint8_t pin);

// 设置GPIO引脚的方向（输入或输出）。
bool gpio_set_direction(uint8_t pin, bool to_output);

// 向一个配置为输出的GPIO引脚写入值。
bool gpio_write(uint8_t pin, bool value);

// 从一个配置为输入的GPIO引脚读取值。
// 返回 0 或 1 表示读取到的值, -1 表示失败。
int8_t gpio_read(uint8_t pin);
}

#endif //MARINELITTERMONITORDEVICE_GPIO_H
