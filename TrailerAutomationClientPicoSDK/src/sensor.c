#include "sensor.h"
#include "config.h"
#include "logging.h"
#include "network.h"
#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "hardware/i2c.h"
#include "lwip/pbuf.h"
#include "lwip/tcp.h"
#include <string.h>
#include <stdio.h>
#include <math.h>

// SHT31 I2C configuration
#define SHT31_I2C_ADDR 0x44
#define SHT31_I2C_PORT i2c0
#define SHT31_SDA_PIN 4
#define SHT31_SCL_PIN 5

// SHT31 commands
#define SHT31_MEASURE_HIGHREP 0x2400

static bool sensor_initialized = false;

// Simple HTTP POST implementation for sensor reading
static struct tcp_pcb *sensor_pcb = NULL;
static bool sensor_complete = false;
static bool sensor_success = false;

static err_t sensor_recv(void *arg, struct tcp_pcb *tpcb, struct pbuf *p, err_t err) {
    if (p == NULL) {
        tcp_close(tpcb);
        sensor_complete = true;
        return ERR_OK;
    }

    // Check for 2xx status code
    char *data = (char *)p->payload;
    if (strncmp(data, "HTTP/1.", 7) == 0) {
        int status_code = 0;
        if (sscanf(data + 9, "%d", &status_code) == 1) {
            if (status_code >= 200 && status_code < 300) {
                sensor_success = true;
                log_line("Sensor response: success");
            } else {
                char msg[64];
                snprintf(msg, sizeof(msg), "Sensor response code: %d", status_code);
                log_line(msg);
            }
        }
    }

    tcp_recved(tpcb, p->tot_len);
    pbuf_free(p);
    tcp_close(tpcb);
    sensor_complete = true;

    return ERR_OK;
}

static err_t sensor_connected(void *arg, struct tcp_pcb *tpcb, err_t err) {
    if (err != ERR_OK) {
        log_line("Sensor connection failed");
        sensor_complete = true;
        return err;
    }

    float temp = 0.0f, hum = 0.0f;
    read_temperature_and_humidity(&temp, &hum);

    // Build JSON payload
    char payload[256];
    snprintf(payload, sizeof(payload),
             "{\"ClientId\":\"%s\",\"TemperatureC\":%.2f,\"HumidityPercent\":%.2f}",
             CLIENT_ID, temp, hum);

    char request[512];
    int len = snprintf(request, sizeof(request),
                      "POST /api/sensor-readings HTTP/1.1\r\n"
                      "Host: %s:%u\r\n"
                      "Content-Type: application/json\r\n"
                      "Content-Length: %d\r\n"
                      "Connection: close\r\n"
                      "\r\n"
                      "%s",
                      get_gateway_host(),
                      get_gateway_port(),
                      (int)strlen(payload),
                      payload);

    err_t write_err = tcp_write(tpcb, request, len, TCP_WRITE_FLAG_COPY);
    if (write_err != ERR_OK) {
        log_line("Sensor tcp_write failed");
        tcp_close(tpcb);
        sensor_complete = true;
        return write_err;
    }

    tcp_output(tpcb);
    return ERR_OK;
}

void init_sensor(void) {
    log_line("Initializing SHT31 sensor...");
    
    // Initialize I2C
    i2c_init(SHT31_I2C_PORT, 100 * 1000);  // 100 kHz
    gpio_set_function(SHT31_SDA_PIN, GPIO_FUNC_I2C);
    gpio_set_function(SHT31_SCL_PIN, GPIO_FUNC_I2C);
    gpio_pull_up(SHT31_SDA_PIN);
    gpio_pull_up(SHT31_SCL_PIN);

    // Try to communicate with sensor
    uint8_t buf[2] = {0x24, 0x00};  // High repeatability measurement
    int result = i2c_write_timeout_us(SHT31_I2C_PORT, SHT31_I2C_ADDR, buf, 2, false, 10000);
    
    if (result == 2) {
        log_line("SHT31 sensor initialized successfully.");
        sensor_initialized = true;
    } else {
        log_line("ERROR: Could not find SHT31 sensor!");
        log_line("Check wiring: SDA(GP4), SCL(GP5), VCC, GND");
        sensor_initialized = false;
    }
}

void read_temperature_and_humidity(float* temperature_c, float* humidity_percent) {
    *temperature_c = 0.0f;
    *humidity_percent = 0.0f;

    if (!sensor_initialized) {
        log_line("ERROR: Sensor not initialized!");
        return;
    }

    // Send measurement command (high repeatability)
    uint8_t cmd[2] = {0x24, 0x00};
    if (i2c_write_timeout_us(SHT31_I2C_PORT, SHT31_I2C_ADDR, cmd, 2, false, 10000) != 2) {
        log_line("ERROR: Failed to send measurement command to SHT31");
        return;
    }

    // Wait for measurement to complete
    sleep_ms(20);

    // Read 6 bytes: temp MSB, temp LSB, temp CRC, hum MSB, hum LSB, hum CRC
    uint8_t data[6];
    if (i2c_read_timeout_us(SHT31_I2C_PORT, SHT31_I2C_ADDR, data, 6, false, 10000) != 6) {
        log_line("ERROR: Failed to read from SHT31 sensor");
        return;
    }

    // Convert temperature
    uint16_t temp_raw = (data[0] << 8) | data[1];
    *temperature_c = -45.0f + (175.0f * temp_raw / 65535.0f);

    // Convert humidity
    uint16_t hum_raw = (data[3] << 8) | data[4];
    *humidity_percent = 100.0f * hum_raw / 65535.0f;

    // Check for invalid values
    if (isnan(*temperature_c) || isnan(*humidity_percent)) {
        log_line("ERROR: Invalid sensor readings (NaN)");
        *temperature_c = 0.0f;
        *humidity_percent = 0.0f;
    }
}

bool send_sensor_reading(void) {
    if (!is_gateway_known()) {
        log_line("send_sensor_reading() called but gateway is unknown.");
        return false;
    }

    if (cyw43_wifi_link_status(&cyw43_state, CYW43_ITF_STA) != CYW43_LINK_UP) {
        log_line("send_sensor_reading() called but Wi-Fi is not connected.");
        return false;
    }

    float temp = 0.0f, hum = 0.0f;
    read_temperature_and_humidity(&temp, &hum);

    char msg[128];
    snprintf(msg, sizeof(msg), "Sensor reading: TempC=%.2f Humidity=%.2f", temp, hum);
    log_line(msg);

    snprintf(msg, sizeof(msg), "Sending sensor reading to http://%s:%u/api/sensor-readings",
             get_gateway_host(), get_gateway_port());
    log_line(msg);

    // Reset state
    sensor_complete = false;
    sensor_success = false;

    // Parse gateway IP address
    ip_addr_t gateway_addr;
    if (!ipaddr_aton(get_gateway_host(), &gateway_addr)) {
        log_line("Failed to parse gateway IP address");
        return false;
    }

    cyw43_arch_lwip_begin();
    
    sensor_pcb = tcp_new();
    if (sensor_pcb == NULL) {
        log_line("Failed to create TCP PCB for sensor reading");
        cyw43_arch_lwip_end();
        return false;
    }

    tcp_recv(sensor_pcb, sensor_recv);
    
    err_t err = tcp_connect(sensor_pcb, &gateway_addr, get_gateway_port(), sensor_connected);
    
    cyw43_arch_lwip_end();

    if (err != ERR_OK) {
        log_line("Sensor tcp_connect failed");
        return false;
    }

    // Wait for completion (with timeout)
    uint32_t start = to_ms_since_boot(get_absolute_time());
    while (!sensor_complete && (to_ms_since_boot(get_absolute_time()) - start) < 5000) {
        cyw43_arch_poll();
        sleep_ms(10);
    }

    if (!sensor_complete) {
        log_line("Sensor reading timeout");
        cyw43_arch_lwip_begin();
        tcp_abort(sensor_pcb);
        cyw43_arch_lwip_end();
        return false;
    }

    return sensor_success;
}
