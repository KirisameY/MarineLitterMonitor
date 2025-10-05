#!/bin/bash

# 获取脚本所在的绝对路径
# 这行代码可以确保无论你从哪里执行这个脚本，路径都是正确的
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)

# 构造 C++ 库的绝对路径
CPP_LIB_DIR="$SCRIPT_DIR/cpp_lib"

echo "Starting .NET application..."
echo "LD_LIBRARY_PATH is set to: $LD_LIBRARY_PATH"

# 以管理员权限运行.net程序，同时设置 LD_LIBRARY_PATH 环境变量
# 将我们的库路径添加到现有路径的最前面
# 这样可以优先搜索我们的库，同时不破坏系统已有的设置
sudo LD_LIBRARY_PATH="$CPP_LIB_DIR" dotnet "$SCRIPT_DIR/dotnet-app/MarineLitterMonitorServer.dll"
