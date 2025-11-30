#ifndef SENSOR_H
#define SENSOR_H

#include <stdbool.h>

// Initialize the SHT31 sensor
void init_sensor(void);

// Send a temperature/humidity reading to /api/sensor-readings
bool send_sensor_reading(void);

// Read temperature and humidity from SHT31 sensor
void read_temperature_and_humidity(float* temperature_c, float* humidity_percent);

#endif // SENSOR_H
