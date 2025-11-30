#include <stdio.h>
#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "config.h"
#include "logging.h"
#include "network.h"
#include "heartbeat.h"
#include "sensor.h"

// Timers for periodic tasks
static uint32_t g_last_heartbeat_ms = 0;
static uint32_t g_last_sensor_ms = 0;

int main(void) {
    stdio_init_all();
    sleep_ms(1000);

    log_line("TrailerAutomationClientPicoSDK starting...");
    
    // Initialize CYW43 WiFi chip
    if (cyw43_arch_init()) {
        log_line("Failed to initialize CYW43");
        return 1;
    }

    log_line("CYW43 initialized.");
    
    // Initialize SHT31 sensor
    init_sensor();
    
    log_line("Initializing Wi-Fi...");

    // Initial Wi-Fi connect
    if (!ensure_wifi_connected()) {
        log_line("Initial Wi-Fi connection failed, will retry in loop.");
    }

    uint32_t now = to_ms_since_boot(get_absolute_time());
    g_last_heartbeat_ms = now;
    g_last_sensor_ms = now;

    log_line("Entering main loop...");

    // Main loop
    while (true) {
        // 1. Ensure Wi-Fi
        if (!ensure_wifi_connected()) {
            if (is_gateway_known()) {
                log_line("Lost Wi-Fi; forgetting previously known gateway.");
                forget_gateway();
            }
            sleep_ms(1000);
            continue;
        }

        // 2. Ensure gateway discovery
        if (!is_gateway_known()) {
            log_line("Gateway not known, attempting mDNS discovery...");
            if (!discover_gateway()) {
                log_line("Gateway discovery attempt failed; will retry.");
                sleep_ms(2000);
                continue;
            }
            log_line("Gateway discovered, will start heartbeat and sensor loops.");
        }

        // 3. Heartbeat timing
        now = to_ms_since_boot(get_absolute_time());
        if (now - g_last_heartbeat_ms >= HEARTBEAT_INTERVAL_MS) {
            if (!send_heartbeat()) {
                log_line("Heartbeat failed. Will keep trying.");
            }
            g_last_heartbeat_ms = now;
        }

        // 4. Sensor timing (independent of heartbeat)
        if (now - g_last_sensor_ms >= SENSOR_INTERVAL_MS) {
            if (!send_sensor_reading()) {
                log_line("Sensor reading failed. Will keep trying.");
            }
            g_last_sensor_ms = now;
        }

        // 5. Small delay to avoid busy spin
        sleep_ms(10);
    }
    
    // Cleanup (unreachable in normal operation)
    cyw43_arch_deinit();
    return 0;
}
