#ifndef MARINELITTERMONITORDEVICE_LIBRARY_H
#define MARINELITTERMONITORDEVICE_LIBRARY_H

#include <cstdint>

void hello();

extern "C" {
int32_t mlmd_test_add_1(int32_t value);
}

#endif //MARINELITTERMONITORDEVICE_LIBRARY_H
