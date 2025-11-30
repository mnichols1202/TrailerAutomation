#ifndef HEARTBEAT_H
#define HEARTBEAT_H

#include <stdbool.h>

// Sends a heartbeat to the gateway's /api/heartbeat endpoint
// Returns true on success (2xx), false otherwise.
bool send_heartbeat(void);

#endif // HEARTBEAT_H
