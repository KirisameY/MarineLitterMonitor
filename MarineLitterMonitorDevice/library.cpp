#include "library.h"

#include <iostream>

void hello()
{
    std::cout << "Hello, World!" << std::endl;
}

extern "C" {
int32_t mlmd_test_add_1(int32_t value)
{
    return value + 1;
}
}
