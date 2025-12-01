#include "power.h"
#include <Arduino.h>

// ESP32-S3 internal voltage monitoring
// Without external voltage divider, we can't directly measure USB/battery voltage
// However, we can monitor for brownout conditions and WiFi stability issues
// that correlate with power problems

static bool power_monitoring_initialized = false;
static uint32_t g_bootVoltageBaseline = 0;

void initPowerMonitoring()
{
    power_monitoring_initialized = true;
    
    // Configure ADC pin
    pinMode(POWER_ADC_PIN, INPUT);
    
    // Read baseline voltage for comparison
    uint32_t reading = 0;
    for (int i = 0; i < 20; i++)
    {
        reading += analogRead(POWER_ADC_PIN);
        delay(10);
    }
    g_bootVoltageBaseline = reading / 20;
    
    Serial.print("[Power] Power monitoring initialized on GPIO");
    Serial.print(POWER_ADC_PIN);
    Serial.print(" (baseline: ");
    Serial.print(g_bootVoltageBaseline);
    Serial.println(")");
    Serial.println("[Power] Note: Without voltage divider circuit, readings are relative only");
}

uint32_t readSupplyVoltage()
{
    if (!power_monitoring_initialized)
    {
        initPowerMonitoring();
    }
    
    // Read multiple samples for stability
    uint32_t adc_reading = 0;
    const int samples = 20;
    
    for (int i = 0; i < samples; i++)
    {
        adc_reading += analogRead(POWER_ADC_PIN);
        delay(5);
    }
    adc_reading /= samples;
    
    // Convert to millivolts
    // With voltage divider (optional): USB 5V -> 10K -> ADC -> 10K -> GND
    // This gives 2.5V at ADC for 5V input
    // ESP32-S3 ADC is 12-bit (0-4095) with 0-3.3V range
    uint32_t voltage_mv = (adc_reading * 3300) / 4095;
    
    // If voltage divider is present, multiply by 2 to get actual voltage
    // Otherwise this is just the pin voltage (not USB voltage)
    voltage_mv *= 2;
    
    return voltage_mv;
}

bool isPowerSufficient()
{
    if (!power_monitoring_initialized)
    {
        return true;  // Not initialized yet, assume OK
    }
    
    // Without voltage divider, we can't measure absolute voltage
    // But we can detect significant drops from baseline
    uint32_t current_reading = 0;
    for (int i = 0; i < 10; i++)
    {
        current_reading += analogRead(POWER_ADC_PIN);
        delay(5);
    }
    current_reading /= 10;
    
    // Check if reading has dropped significantly from baseline (>15%)
    if (g_bootVoltageBaseline > 100)  // Only check if we have meaningful baseline
    {
        int32_t deviation = ((int32_t)g_bootVoltageBaseline - (int32_t)current_reading);
        int32_t percent_change = (deviation * 100) / g_bootVoltageBaseline;
        
        if (percent_change > 15)
        {
            Serial.print("[Power] Warning: Voltage drop detected (");
            Serial.print(percent_change);
            Serial.println("% below baseline)");
            return false;
        }
    }
    
    return true;
}

String getVoltageString()
{
    if (!power_monitoring_initialized)
    {
        return "Not initialized";
    }
    
    uint32_t voltage_mv = readSupplyVoltage();
    
    if (voltage_mv == 0)
    {
        return "0.00V (check wiring)";
    }
    
    float voltage_v = voltage_mv / 1000.0;
    
    char buffer[32];
    snprintf(buffer, sizeof(buffer), "%.2fV (no divider)", voltage_v);
    
    return String(buffer);
}
