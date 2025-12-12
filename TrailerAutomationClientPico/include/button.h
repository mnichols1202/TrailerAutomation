#ifndef BUTTON_H
#define BUTTON_H

#include <Arduino.h>

/**
 * Initialize button GPIO pins and internal state
 * Must be called after fsconfig is loaded
 * Returns true if successful
 */
bool initButtons();

/**
 * Check all buttons for state changes and handle toggle actions
 * Call this in the main loop
 */
void checkButtons();

/**
 * Sync button state tracking when relay state changes externally
 * Call this from command listener after setRelay commands
 */
void syncButtonRelayState(const char* relayId, bool state);

#endif // BUTTON_H
