#include "rgbled.h"
#include <Arduino.h>
#include <Adafruit_NeoPixel.h>

// NeoPixel object
static Adafruit_NeoPixel rgb(RGB_COUNT, RGB_PIN, NEO_GRB + NEO_KHZ800);

// Current state and timing
static LedState g_currentState = LED_OFF;
static LedErrorCode g_errorCode = ERROR_NONE;
static unsigned long g_lastBlinkMs = 0;
static bool g_blinkOn = false;
static unsigned long g_connectedStartMs = 0;
static bool g_connectedTimerActive = false;

// Error blink pattern state
static int g_blinkCount = 0;
static int g_currentBlink = 0;
static unsigned long g_errorCycleStartMs = 0;

// Blink intervals (milliseconds)
static const unsigned long BLINK_INTERVAL_CONNECTING = 500;  // Yellow blink every 500ms
static const unsigned long ERROR_BLINK_ON_TIME = 500;        // 0.5 sec on
static const unsigned long ERROR_BLINK_OFF_TIME = 500;       // 0.5 sec off
static const unsigned long ERROR_CYCLE_PAUSE = 1000;         // 1 sec pause between cycles
static const unsigned long CONNECTED_DISPLAY_TIME = 5000;    // Show green for 5 seconds

void initRgbLed()
{
    rgb.begin();
    rgb.clear();
    rgb.setBrightness(RGB_BRIGHTNESS);
    rgb.show();
}

// Helper function to set RGB color
static void setRgbColor(uint8_t r, uint8_t g, uint8_t b)
{
    rgb.setPixelColor(0, rgb.Color(r, g, b));
    rgb.show();
}

void setLedState(LedState state)
{
    g_currentState = state;
    g_errorCode = ERROR_NONE;  // Clear error code when setting normal state
    
    // Reset blink state when changing states
    g_blinkOn = false;
    g_lastBlinkMs = millis();
    
    // Handle solid colors immediately
    switch (state)
    {
        case LED_OFF:
            setRgbColor(0, 0, 0);
            g_connectedTimerActive = false;
            break;
            
        case LED_BOOT:
            // Solid red
            setRgbColor(255, 0, 0);
            g_connectedTimerActive = false;
            break;
            
        case LED_CONNECTED:
            // Solid green - start timer
            setRgbColor(0, 255, 0);
            g_connectedStartMs = millis();
            g_connectedTimerActive = true;
            break;
            
        case LED_CONNECTING:
            // Solid blue
            setRgbColor(0, 0, 255);
            g_connectedTimerActive = false;
            break;
            
        case LED_ERROR:
            // Blinking state handled in updateLed()
            g_connectedTimerActive = false;
            break;
    }
}

void setLedError(LedErrorCode errorCode)
{
    g_currentState = LED_ERROR;
    g_errorCode = errorCode;
    g_blinkCount = errorCode;
    g_currentBlink = 0;
    g_errorCycleStartMs = millis();
    g_blinkOn = false;
    g_connectedTimerActive = false;
    setRgbColor(0, 0, 0);  // Start with LED off
}

void clearLedError()
{
    if (g_currentState == LED_ERROR && g_errorCode != ERROR_NONE)
    {
        g_errorCode = ERROR_NONE;
        setLedState(LED_OFF);
    }
}

void updateLed()
{
    unsigned long now = millis();
    
    // Handle connected timer - turn off after 5 seconds
    if (g_connectedTimerActive)
    {
        if (now - g_connectedStartMs >= CONNECTED_DISPLAY_TIME)
        {
            setLedState(LED_OFF);
            g_connectedTimerActive = false;
        }
        return;  // Keep showing solid green until timer expires
    }
    
    // Handle blinking states
    switch (g_currentState)
    {
        case LED_CONNECTING:
            // Solid blue - no blinking needed, already set in setLedState
            break;
            
        case LED_ERROR:
            if (g_errorCode == ERROR_NONE)
            {
                // No error code set, just flash red continuously (legacy behavior)
                if (now - g_lastBlinkMs >= 150)
                {
                    g_blinkOn = !g_blinkOn;
                    setRgbColor(g_blinkOn ? 255 : 0, 0, 0);
                    g_lastBlinkMs = now;
                }
            }
            else
            {
                // Determine color based on error type
                bool isWifiError = (g_errorCode >= ERROR_WIFI_TIMEOUT && g_errorCode <= ERROR_WIFI_GENERIC);
                uint8_t blinkR = isWifiError ? 0 : 255;  // Blue for WiFi, Red for others
                uint8_t blinkG = 0;
                uint8_t blinkB = isWifiError ? 255 : 0;
                
                // Error code pattern: blink N times, pause, repeat
                unsigned long elapsed = now - g_errorCycleStartMs;
                
                if (g_currentBlink < g_blinkCount)
                {
                    // Calculate position within current blink
                    unsigned long blinkTime = ERROR_BLINK_ON_TIME + ERROR_BLINK_OFF_TIME;
                    unsigned long totalBlinkTime = g_currentBlink * blinkTime;
                    unsigned long timeInBlink = elapsed - totalBlinkTime;
                    
                    if (timeInBlink < ERROR_BLINK_ON_TIME)
                    {
                        // LED should be ON
                        if (!g_blinkOn)
                        {
                            setRgbColor(blinkR, blinkG, blinkB);  // Blue for WiFi, Red for others
                            g_blinkOn = true;
                        }
                    }
                    else if (timeInBlink < blinkTime)
                    {
                        // LED should be OFF
                        if (g_blinkOn)
                        {
                            setRgbColor(0, 0, 0);  // Off
                            g_blinkOn = false;
                        }
                    }
                    else
                    {
                        // Move to next blink
                        g_currentBlink++;
                    }
                }
                else
                {
                    // All blinks done, wait for pause
                    unsigned long totalBlinkTime = g_blinkCount * (ERROR_BLINK_ON_TIME + ERROR_BLINK_OFF_TIME);
                    
                    if (elapsed >= totalBlinkTime + ERROR_CYCLE_PAUSE)
                    {
                        // Restart cycle
                        g_currentBlink = 0;
                        g_errorCycleStartMs = now;
                        g_blinkOn = false;
                        setRgbColor(0, 0, 0);
                    }
                    else
                    {
                        // Keep LED off during pause
                        if (g_blinkOn)
                        {
                            setRgbColor(0, 0, 0);
                            g_blinkOn = false;
                        }
                    }
                }
            }
            break;
            
        default:
            // LED_OFF, LED_BOOT, LED_CONNECTED are solid or off, no update needed
            break;
    }
}
