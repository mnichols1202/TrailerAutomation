#include "otaupdate.h"
#include "logging.h"
#include "rgbled.h"
#include <WiFi.h>
#include <WiFiClient.h>
#include <HTTPUpdate.h>

bool performOtaUpdate(const char* url)
{
    logLine("[OTA] Starting OTA update from: " + String(url));
    setLedState(LED_CONNECTING);  // Solid blue while downloading

    WiFiClient client;
    client.setTimeout(30000);  // 30 s timeout on the underlying socket

    httpUpdate.onStart([]() {
        logLine("[OTA] Download started");
    });
    httpUpdate.onProgress([](int received, int total) {
        if (total > 0 && received % (total / 10 + 1) < 4096)
            logLine("[OTA] Progress: " + String(received * 100 / total) + "%");
    });
    httpUpdate.onError([](int err) {
        logLine("[OTA] Error: " + String(err));
    });

    t_httpUpdate_return ret = httpUpdate.update(client, url);

    switch (ret)
    {
        case HTTP_UPDATE_OK:
            // httpUpdate triggers reboot automatically; this line is never reached
            return true;

        case HTTP_UPDATE_NO_UPDATES:
            logLine("[OTA] No update available");
            return false;

        case HTTP_UPDATE_FAILED:
        default:
            logLine("[OTA] Update failed: " + httpUpdate.getLastErrorString());
            return false;
    }
}
