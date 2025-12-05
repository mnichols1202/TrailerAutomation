#include "logging.h"
#include "config.h"

void logLine(const String& msg)
{
#if DEBUG_LOGGING
    Serial.print("[");
    Serial.print(millis());
    Serial.print(" ms] ");
    Serial.println(msg);
#else
    // No-op in production builds for performance
    (void)msg; // Suppress unused parameter warning
#endif
}
