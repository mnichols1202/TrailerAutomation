#pragma once

// Sends a heartbeat to the gateway's /api/heartbeat endpoint
// Returns true on success, false on failure.
// wasRegistered: pass true if device has completed initial registration
bool sendHeartbeat(bool wasRegistered = false);
