#pragma once

// Sends a heartbeat to the gateway's /api/heartbeat endpoint
// Returns true on success (2xx), false otherwise.
bool sendHeartbeat();
