#pragma once

// Sends a heartbeat to the gateway's /api/heartbeat endpoint
// Returns true if gateway requests re-registration, false otherwise.
bool sendHeartbeat();
