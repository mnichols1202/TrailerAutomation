#pragma once

// Send a temperature/humidity reading to /api/sensor-readings
bool sendSensorReading();

// Stub for reading temperature and humidity.
// Replace this later with real SHT31 (or other) sensor code.
void readTemperatureAndHumidity(float& temperatureC, float& humidityPercent);
