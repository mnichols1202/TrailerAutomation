#pragma once

// Initialize the SHT31 sensor
void initSensor();

// Check if sensor is available and initialized
bool isSensorAvailable();

// Send a temperature/humidity reading to /api/sensor-readings
bool sendSensorReading();

// Read temperature and humidity from SHT31 sensor
// Returns true if successful, false if sensor read failed
bool readTemperatureAndHumidity(float& temperatureC, float& humidityPercent);
