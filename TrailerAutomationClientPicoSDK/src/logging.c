#include "logging.h"

void log_line(const char* msg) {
    uint32_t now = to_ms_since_boot(get_absolute_time());
    printf("[%lu ms] %s\n", now, msg);
}
