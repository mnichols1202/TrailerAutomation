#ifndef COMMANDLISTENER_H
#define COMMANDLISTENER_H

#include <Arduino.h>

/**
 * Initialize the TCP command listener server
 * Returns true if successful, false otherwise
 */
bool initCommandListener();

/**
 * Process incoming command connections (call in loop)
 */
void processCommandListener();

/**
 * Stop the command listener (cleanup)
 */
void stopCommandListener();

#endif // COMMANDLISTENER_H
