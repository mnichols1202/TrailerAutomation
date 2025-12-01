#pragma once

#include <Arduino.h>

// Power monitoring for ESP32-S3
// Monitors internal voltage reference for power stability
// Uses GPIO4 (ADC1_3) to read voltage reference
// NOTE: For accurate USB/battery voltage measurement, connect voltage divider:
//   USB+ -> 10K resistor -> GPIO4 -> 10K resistor -> GND
// This gives 2.5V at GPIO4 for 5V input (within ADC's 0-3.3V range)

#define POWER_ADC_PIN 4  // GPIO4 is ADC1_3

// Initialize power monitoring
void initPowerMonitoring();

// Read current supply voltage in millivolts
// Returns voltage in mV (e.g., 5000 = 5.0V)
// Returns 0 if reading failed
uint32_t readSupplyVoltage();

// Check if voltage is sufficient for reliable operation
// Returns true if voltage is adequate, false if too low
bool isPowerSufficient();

// Get voltage as formatted string (e.g., "4.85V")
String getVoltageString();
