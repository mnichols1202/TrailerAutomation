#include "logging.h"

void logLine(const String& msg)
{
    Serial.print("[");
    Serial.print(millis());
    Serial.print(" ms] ");
    Serial.println(msg);
}
