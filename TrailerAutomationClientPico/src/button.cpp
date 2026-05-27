#include "button.h"
#include "fsconfig.h"
#include "relaycontrol.h"
#include "logging.h"
#include "network.h"
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

// Button polling runs on core 1 (loop1); all network I/O must stay on core 0.
// On a press edge, core 1 toggles the relay GPIO immediately (light turns on),
// then enqueues a notification. Core 0 drains the queue and POSTs to the
// gateway when it can. If the gateway is offline, the notification is dropped
// — the relay is already in the correct physical state.

struct ButtonState
{
    int pin;
    bool lastValue;          // raw reading, for debounce detection
    bool lastStableValue;    // post-debounce stable reading
    unsigned long lastDebounceTime;
    char targetDevice[MAX_DEVICE_ID_LEN];
    char targetRelay[MAX_RELAY_ID_LEN];
    volatile bool relayState;  // mirrors physical relay; touched by both cores
};

static ButtonState g_buttonStates[MAX_BUTTONS];
static int g_buttonCount = 0;
static bool g_buttonsInitialized = false;

#define DEBOUNCE_DELAY_MS 50

// ---------------------------------------------------------------------------
// Cross-core SPSC notification queue
// ---------------------------------------------------------------------------
// Producer: core 1 (button polling). Consumer: core 0 (main loop).
// 32-bit aligned loads/stores are atomic on Cortex-M0+ and M33;
// __sync_synchronize() guarantees slot contents are visible before the
// head index is published.

#define NOTIFY_QUEUE_SIZE 16

enum NotifyKind : uint8_t
{
    NOTIFY_LOCAL_STATE   = 1,  // POST /api/devices/{me}/relays/{id}/state?state=...
    NOTIFY_REMOTE_TOGGLE = 2   // POST /api/devices/{other}/relays/{id}/toggle
};

struct PendingNotify
{
    uint8_t kind;
    bool    state;                                   // only meaningful for LOCAL_STATE
    char    targetDevice[MAX_DEVICE_ID_LEN];
    char    relayId[MAX_RELAY_ID_LEN];
};

static PendingNotify   g_notifyQueue[NOTIFY_QUEUE_SIZE];
static volatile uint8_t g_notifyHead = 0;   // written by core 1
static volatile uint8_t g_notifyTail = 0;   // written by core 0

static void enqueueNotify(const PendingNotify& n)
{
    uint8_t head = g_notifyHead;
    uint8_t next = (head + 1) % NOTIFY_QUEUE_SIZE;
    if (next == g_notifyTail)
    {
        // Queue full — drop this notification. SPSC discipline: only the
        // consumer (core 0) advances tail. The relay is already in the
        // correct physical state; the gateway just misses one update.
        logLine("[Notify] Queue full, dropping notification for " + String(n.relayId));
        return;
    }
    g_notifyQueue[head] = n;
    __sync_synchronize();
    g_notifyHead = next;
}

static bool dequeueNotify(PendingNotify& out)
{
    uint8_t tail = g_notifyTail;
    if (tail == g_notifyHead) return false;
    out = g_notifyQueue[tail];
    __sync_synchronize();
    g_notifyTail = (tail + 1) % NOTIFY_QUEUE_SIZE;
    return true;
}

// ---------------------------------------------------------------------------

bool initButtons()
{
    if (!isFsConfigLoaded())
    {
        logLine("ERROR: Cannot initialize buttons - config not loaded");
        return false;
    }

    const DeviceConfig& config = getDeviceConfig();
    g_buttonCount = config.buttonCount;

    if (g_buttonCount == 0)
    {
        logLine("No buttons configured");
        g_buttonsInitialized = true;
        return true;
    }

    logLine("Initializing " + String(g_buttonCount) + " button(s)...");

    for (int i = 0; i < g_buttonCount; i++)
    {
        const ButtonConfig& btn = config.buttons[i];

        if (!btn.enabled)
        {
            logLine("Button '" + String(btn.name) + "' disabled, skipping");
            continue;
        }

        // Active LOW with internal pull-up
        pinMode(btn.pin, INPUT_PULLUP);

        g_buttonStates[i].pin = btn.pin;
        g_buttonStates[i].lastValue = HIGH;
        g_buttonStates[i].lastStableValue = HIGH;
        g_buttonStates[i].lastDebounceTime = 0;
        strncpy(g_buttonStates[i].targetDevice, btn.targetDevice, MAX_DEVICE_ID_LEN - 1);
        strncpy(g_buttonStates[i].targetRelay, btn.targetRelay, MAX_RELAY_ID_LEN - 1);

        bool actualState = false;
        if (strcmp(btn.targetDevice, config.clientId) == 0)
        {
            getRelayState(btn.targetRelay, &actualState);
        }
        g_buttonStates[i].relayState = actualState;

        logLine("Button '" + String(btn.name) + "' initialized on pin " + String(btn.pin) +
                " -> " + String(btn.targetDevice) + ":" + String(btn.targetRelay));
    }

    g_buttonsInitialized = true;
    logLine("Button initialization complete");
    return true;
}

static void handleLocalToggle(ButtonState* btnState)
{
    // Toggle local relay state (mirrors web UI behavior).
    bool newState = !btnState->relayState;

    if (setRelayState(btnState->targetRelay, newState))
    {
        btnState->relayState = newState;
        logLine("[Button] Set local relay '" + String(btnState->targetRelay) + "' to " + (newState ? "ON" : "OFF"));

        // Light is already in the right state. Hand off the gateway notification
        // to core 0 so we never block button polling on network I/O.
        const DeviceConfig& config = getDeviceConfig();
        PendingNotify n = {};
        n.kind  = NOTIFY_LOCAL_STATE;
        n.state = newState;
        strncpy(n.targetDevice, config.clientId, MAX_DEVICE_ID_LEN - 1);
        strncpy(n.relayId,      btnState->targetRelay, MAX_RELAY_ID_LEN - 1);
        enqueueNotify(n);
    }
    else
    {
        logLine("[Button] ERROR: Failed to set relay state for '" + String(btnState->targetRelay) + "'");
    }
}

static void handleRemoteToggle(ButtonState* btnState)
{
    // Remote toggle goes through the gateway. Queue it for core 0 — if the
    // gateway is offline, this notification will be dropped during drain.
    PendingNotify n = {};
    n.kind  = NOTIFY_REMOTE_TOGGLE;
    n.state = false;
    strncpy(n.targetDevice, btnState->targetDevice, MAX_DEVICE_ID_LEN - 1);
    strncpy(n.relayId,      btnState->targetRelay, MAX_RELAY_ID_LEN - 1);
    enqueueNotify(n);

    logLine("[Button] Queued remote toggle for " + String(btnState->targetDevice) + ":" + String(btnState->targetRelay));
}

void checkButtons()
{
    if (!g_buttonsInitialized || g_buttonCount == 0)
    {
        return;
    }

    const DeviceConfig& config = getDeviceConfig();
    unsigned long now = millis();

    for (int i = 0; i < g_buttonCount; i++)
    {
        const ButtonConfig& btn = config.buttons[i];

        if (!btn.enabled)
        {
            continue;
        }

        ButtonState& state = g_buttonStates[i];

        bool currentReading = digitalRead(state.pin);

        if (currentReading != state.lastValue)
        {
            state.lastDebounceTime = now;
            state.lastValue = currentReading;
        }

        if ((now - state.lastDebounceTime) > DEBOUNCE_DELAY_MS)
        {
            // Rising edge after debounce: button was pressed and released.
            if (state.lastStableValue == LOW && currentReading == HIGH)
            {
                logLine("[Button] Button '" + String(btn.name) + "' pressed");

                if (strcmp(state.targetDevice, config.clientId) == 0)
                {
                    handleLocalToggle(&state);
                }
                else
                {
                    handleRemoteToggle(&state);
                }
            }

            state.lastStableValue = currentReading;
        }
    }
}

void syncButtonRelayState(const char* relayId, bool state)
{
    if (!g_buttonsInitialized)
    {
        return;
    }

    for (int i = 0; i < g_buttonCount; i++)
    {
        ButtonState& btnState = g_buttonStates[i];

        if (strcmp(btnState.targetRelay, relayId) == 0)
        {
            btnState.relayState = state;
            logLine("[Button] Synced button state for relay '" + String(relayId) + "' to " + (state ? "ON" : "OFF"));
        }
    }
}

void drainPendingNotifications()
{
    // Send at most one notification per call so we don't stall the main loop
    // when the queue is full. Network I/O only happens here (core 0).
    PendingNotify n;
    if (!dequeueNotify(n)) return;

    if (!isGatewayKnown() || WiFi.status() != WL_CONNECTED)
    {
        logLine("[Notify] Gateway offline, dropping " + String(n.relayId));
        return;
    }

    String url = "http://" + getGatewayHost() + ":" + String(getGatewayPort()) +
                 "/api/devices/" + String(n.targetDevice) +
                 "/relays/" + String(n.relayId);

    if (n.kind == NOTIFY_LOCAL_STATE)
    {
        url += "/state?state=";
        url += (n.state ? "on" : "off");
    }
    else  // NOTIFY_REMOTE_TOGGLE
    {
        url += "/toggle";
    }

    HTTPClient http;
    http.begin(url);
    http.setTimeout(2000);   // short — must not stall the main loop
    int httpCode = http.POST("");

    if (httpCode == HTTP_CODE_OK)
    {
        logLine("[Notify] Sent " + String(n.kind == NOTIFY_LOCAL_STATE ? "state" : "toggle") +
                " for " + String(n.relayId));
    }
    else
    {
        logLine("[Notify] HTTP " + String(httpCode) + " for " + String(n.relayId));
    }

    http.end();
}
