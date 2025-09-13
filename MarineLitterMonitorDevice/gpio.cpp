#include "gpio.h"

#include <chrono>
#include <iostream>
#include <fstream>
#include <filesystem>
#include <string>
#include <stdexcept>
#include <thread>


void write_to_file(const std::string& path, const std::string& value)
{
    std::ofstream file(path);
    if (!file.is_open())
    {
        throw std::runtime_error("[C++] Error: Failed to open file for writing: " + path);
    }
    file << value;
    file.close();
}

std::string read_from_file(const std::string& path)
{
    std::ifstream file(path);
    if (!file.is_open())
    {
        throw std::runtime_error("[C++] Error: Failed to open file for reading: " + path);
    }
    std::string value;
    file >> value;
    file.close();
    return value;
}

/**
 * @brief 等待指定路径出现，带有超时。
 * @param path_to_wait_for 要等待的文件或目录路径。
 * @param timeout_ms 超时时间（毫秒）。
 * @return true 如果路径在超时前出现, false 如果超时。
 */
bool wait_for_path(const std::string& path_to_wait_for, const int timeout_ms)
{
    const auto start_time = std::chrono::steady_clock::now();
    while (true)
    {
        if (std::filesystem::exists(path_to_wait_for))
        {
            return true;
        }

        auto current_time = std::chrono::steady_clock::now();
        auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(current_time - start_time).count();
        if (elapsed_ms >= timeout_ms)
        {
            std::cerr << "[C++] Warning: Timeout waiting for path: " << path_to_wait_for << std::endl;
            return false;
        }

        // 等待一个很短的时间再检查，避免 CPU 占用过高
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
}


bool gpio_export(const uint8_t pin)
{
    try
    {
        const std::string pin_str = std::to_string(pin);
        const std::string export_path = "/sys/class/gpio/gpio" + pin_str;

        if (std::filesystem::exists(export_path))
        {
            return true; // 已经导出，视为成功
        }

        write_to_file("/sys/class/gpio/export", pin_str);

        // 我们等待 direction 文件出现，因为它通常是最后被创建的
        const std::string direction_path = export_path + "/direction";
        return wait_for_path(direction_path, 1000); // 等待最多1秒
    }
    catch (const std::exception& e)
    {
        std::cerr << "[C++] Error: Error in gpio_export: " << e.what() << std::endl;
        return false;
    }
}


bool gpio_unexport(const uint8_t pin)
{
    try
    {
        const std::string pin_str = std::to_string(pin);
        const std::string export_path = "/sys/class/gpio/gpio" + pin_str;

        if (!std::filesystem::exists(export_path))
        {
            return true; // 已经取消导出，视为成功
        }

        write_to_file("/sys/class/gpio/unexport", pin_str);
        return true;
    }
    catch (const std::exception& e)
    {
        std::cerr << "[C++] Error: Error in gpio_unexport: " << e.what() << std::endl;
        return false;
    }
}

bool gpio_set_direction(const uint8_t pin, const bool to_output)
{
    try
    {
        const std::string dir_str = to_output ? "out" : "in";
        const std::string path = "/sys/class/gpio/gpio" + std::to_string(pin) + "/direction";
        write_to_file(path, dir_str);
        return true;
    }
    catch (const std::exception& e)
    {
        std::cerr << "[C++] Error: Error in gpio_set_direction: " << e.what() << std::endl;
        return false;
    }
}

bool gpio_write(const uint8_t pin, const bool value)
{
    try
    {
        const std::string path = "/sys/class/gpio/gpio" + std::to_string(pin) + "/value";
        write_to_file(path, value ? "1" : "0");
        return true;
    }
    catch (const std::exception& e)
    {
        std::cerr << "[C++] Error: Error in gpio_write: " << e.what() << std::endl;
        return false;
    }
}

int8_t gpio_read(const uint8_t pin)
{
    try
    {
        const std::string path = "/sys/class/gpio/gpio" + std::to_string(pin) + "/value";
        const std::string value_str = read_from_file(path);
        return static_cast<int8_t>(std::stoi(value_str));
    }
    catch (const std::exception& e)
    {
        std::cerr << "[C++] Error: Error in gpio_read: " << e.what() << std::endl;
        return -1;
    }
}
