cmake_minimum_required(VERSION 3.30)
project(MarineLitterMonitorDevice)

set(CMAKE_CXX_STANDARD 20)

add_library(MarineLitterMonitorDevice SHARED library.cpp)

# CMAKE_SOURCE_DIR 是你项目根目录的绝对路径 (包含CMakeLists.txt的目录)
# 我们用它来构造一个相对于项目根目录的路径
set(OUTPUT_PATH "${CMAKE_SOURCE_DIR}/../cpp_lib")

# 为我们的库目标设置输出目录
# 对于共享库 (.so) 和 DLL，使用 LIBRARY_OUTPUT_DIRECTORY
# 对于静态库 (.a)，使用 ARCHIVE_OUTPUT_DIRECTORY
set_target_properties(MarineLitterMonitorDevice PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY "${OUTPUT_PATH}"
        ARCHIVE_OUTPUT_DIRECTORY "${OUTPUT_PATH}"
)

# 打印一条消息，方便调试，确认路径是否正确
message(STATUS "Library output path set to: ${OUTPUT_PATH}")