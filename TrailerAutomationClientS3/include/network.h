#pragma once

#include <Arduino.h>
#include <WiFi.h>

// Ensure Wi-Fi is connected (tries to reconnect if needed)
bool ensureWifiConnected();

// Get the last WiFi error code (WL_* status)
int getLastWifiError();

// Start the mDNS responder (uses CLIENT_ID as hostname)
bool startMdns();

// Discover the TrailerAutomationGateway via mDNS/DNS-SD
bool discoverGateway();

// Gateway state accessors
bool isGatewayKnown();
void forgetGateway();
String getGatewayHost();
uint16_t getGatewayPort();

// Register device with gateway (includes relay config and command port)
bool registerDevice();
