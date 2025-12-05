#ifndef BUTTON_H
#define BUTTON_H

#include <Arduino.h>

/**
 * Initialize button GPIO pins and internal state
 * Must be called after sdconfig is loaded
 * Returns true if successful
 */
bool initButtons();

/**
 * Check all buttons for state changes and handle toggle actions
 * Call this in the main loop
 */
void checkButtons();

#endif // BUTTON_H
