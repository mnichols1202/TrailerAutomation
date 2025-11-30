#include "heartbeat.h"
#include "config.h"
#include "logging.h"
#include "network.h"
#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "lwip/pbuf.h"
#include "lwip/tcp.h"
#include "lwip/dns.h"
#include <string.h>
#include <stdio.h>
#include <stdint.h>

// Simple HTTP POST implementation for heartbeat
static struct tcp_pcb *heartbeat_pcb = NULL;
static bool heartbeat_complete = false;
static bool heartbeat_success = false;

static err_t heartbeat_recv(void *arg, struct tcp_pcb *tpcb, struct pbuf *p, err_t err) {
    if (p == NULL) {
        // Connection closed
        tcp_close(tpcb);
        heartbeat_complete = true;
        return ERR_OK;
    }

    // Check for 2xx status code in response
    char *data = (char *)p->payload;
    if (strncmp(data, "HTTP/1.", 7) == 0) {
        int status_code = 0;
        if (sscanf(data + 9, "%d", &status_code) == 1) {
            if (status_code >= 200 && status_code < 300) {
                heartbeat_success = true;
                log_line("Heartbeat response: success");
            } else {
                char msg[64];
                snprintf(msg, sizeof(msg), "Heartbeat response code: %d", status_code);
                log_line(msg);
            }
        }
    }

    tcp_recved(tpcb, p->tot_len);
    pbuf_free(p);
    tcp_close(tpcb);
    heartbeat_complete = true;

    return ERR_OK;
}

static err_t heartbeat_connected(void *arg, struct tcp_pcb *tpcb, err_t err) {
    if (err != ERR_OK) {
        log_line("Heartbeat connection failed");
        heartbeat_complete = true;
        return err;
    }

    // Build HTTP POST request
    // JSON payload: { "ClientId": "...", "DeviceType": "...", "FriendlyName": "..." }
    char payload[256];
    snprintf(payload, sizeof(payload),
             "{\"ClientId\":\"%s\",\"DeviceType\":\"%s\",\"FriendlyName\":\"%s\"}",
             CLIENT_ID, DEVICE_TYPE, FRIENDLY_NAME);

    char request[512];
    int len = snprintf(request, sizeof(request),
                      "POST /api/heartbeat HTTP/1.1\r\n"
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
        log_line("Heartbeat tcp_write failed");
        tcp_close(tpcb);
        heartbeat_complete = true;
        return write_err;
    }

    tcp_output(tpcb);
    return ERR_OK;
}

bool send_heartbeat(void) {
    if (!is_gateway_known()) {
        log_line("send_heartbeat() called but gateway is unknown.");
        return false;
    }

    if (cyw43_wifi_link_status(&cyw43_state, CYW43_ITF_STA) != CYW43_LINK_UP) {
        log_line("send_heartbeat() called but Wi-Fi is not connected.");
        return false;
    }

    char msg[160];
    snprintf(msg, sizeof(msg), "Sending heartbeat to http://%s:%u/api/heartbeat",
             get_gateway_host(), get_gateway_port());
    log_line(msg);

    // Reset state
    heartbeat_complete = false;
    heartbeat_success = false;

    // Parse gateway IP address
    ip_addr_t gateway_addr;
    if (!ipaddr_aton(get_gateway_host(), &gateway_addr)) {
        log_line("Failed to parse gateway IP address");
        return false;
    }

    cyw43_arch_lwip_begin();
    
    heartbeat_pcb = tcp_new();
    if (heartbeat_pcb == NULL) {
        log_line("Failed to create TCP PCB for heartbeat");
        cyw43_arch_lwip_end();
        return false;
    }

    tcp_recv(heartbeat_pcb, heartbeat_recv);
    
    err_t err = tcp_connect(heartbeat_pcb, &gateway_addr, get_gateway_port(), heartbeat_connected);
    
    cyw43_arch_lwip_end();

    if (err != ERR_OK) {
        log_line("Heartbeat tcp_connect failed");
        return false;
    }

    // Wait for completion (with timeout)
    uint32_t start = to_ms_since_boot(get_absolute_time());
    while (!heartbeat_complete && (to_ms_since_boot(get_absolute_time()) - start) < 5000) {
        cyw43_arch_poll();
        sleep_ms(10);
    }

    if (!heartbeat_complete) {
        log_line("Heartbeat timeout");
        cyw43_arch_lwip_begin();
        tcp_abort(heartbeat_pcb);
        cyw43_arch_lwip_end();
        return false;
    }

    return heartbeat_success;
}
