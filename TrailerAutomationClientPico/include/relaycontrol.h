#ifndef RELAYCONTROL_H
#define RELAYCONTROL_H

#include <Arduino.h>

/**
 * Initialize GPIO relay controller from config
 * Sets up pins as outputs and applies initial states
 */
bool initRelayControl();

/**
 * Set a relay state by relay ID
 * Returns true if successful, false if relay not found
 */
bool setRelayState(const char* relayId, bool state);

/**
 * Get relay state by relay ID
 * Returns true/false for state, or false if not found
 */
bool getRelayState(const char* relayId, bool* outState);

/**
 * Turn off all relays (used for cleanup/shutdown)
 */
void turnOffAllRelays();

#endif // RELAYCONTROL_H
