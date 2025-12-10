#pragma once

// Sends a heartbeat to the gateway's /api/heartbeat endpoint
// Returns true if gateway requests re-registration, false otherwise.
// wasRegistered: pass true if device has completed initial registration
bool sendHeartbeat(bool wasRegistered = false);
