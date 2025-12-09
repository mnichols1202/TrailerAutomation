using System;
using System.Collections.Generic;
using System.Device.Gpio;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// Manages GPIO pins for relay control.
    /// </summary>
    public class GpioRelayController : IDisposable
    {
        private readonly GpioController _gpio;
        private readonly Dictionary<string, int> _relayPins;
        private bool _disposed;

        public GpioRelayController(AppConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _relayPins = new Dictionary<string, int>();
            
            try
            {
                _gpio = new GpioController();

                // Initialize each relay pin from configuration
                foreach (var relay in config.Hardware.Relays)
                {
                    Console.WriteLine($"[GPIO] Initializing {relay.Id} on pin {relay.Pin} (initial: {relay.InitialState})");
                    
                    // Open pin as output
                    _gpio.OpenPin(relay.Pin, PinMode.Output);
                    
                    // Set initial state
                    var initialValue = relay.InitialState ? PinValue.High : PinValue.Low;
                    _gpio.Write(relay.Pin, initialValue);
                    
                    // Track relay ID to pin mapping
                    _relayPins[relay.Id] = relay.Pin;
                }

                Console.WriteLine($"[GPIO] Initialized {_relayPins.Count} relay pin(s)");
                Console.WriteLine($"[GPIO] Available relays: {string.Join(", ", _relayPins.Keys)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPIO] Warning: Failed to initialize GPIO controller: {ex.Message}");
                Console.WriteLine("[GPIO] GPIO control will be simulated (no hardware control)");
                _gpio?.Dispose();
                _gpio = null!;
            }
        }

        /// <summary>
        /// Set a relay to on or off state.
        /// </summary>
        public bool SetRelay(string relayId, string state)
        {
            if (string.IsNullOrEmpty(relayId))
                throw new ArgumentException("RelayId cannot be empty", nameof(relayId));

            if (string.IsNullOrEmpty(state))
                throw new ArgumentException("State cannot be empty", nameof(state));

            // Check if relay exists
            if (!_relayPins.TryGetValue(relayId, out var pin))
            {
                Console.WriteLine($"[GPIO] Relay '{relayId}' not found in configuration");
                return false;
            }

            // If GPIO not available (e.g., running on non-Pi hardware), simulate
            if (_gpio == null)
            {
                Console.WriteLine($"[GPIO] SIMULATED: {relayId} (pin {pin}) -> {state}");
                return true;
            }

            // Parse state
            PinValue value;
            if (string.Equals(state, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = PinValue.High;
            }
            else if (string.Equals(state, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = PinValue.Low;
            }
            else
            {
                Console.WriteLine($"[GPIO] Invalid state '{state}' for relay '{relayId}' (must be 'on' or 'off')");
                return false;
            }

            try
            {
                _gpio.Write(pin, value);
                Console.WriteLine($"[GPIO] {relayId} (pin {pin}) -> {state} ({value})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPIO] Error writing to pin {pin}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current state of a relay.
        /// </summary>
        public string? GetRelayState(string relayId)
        {
            if (!_relayPins.TryGetValue(relayId, out var pin))
                return null;

            if (_gpio == null)
                return "unknown";

            try
            {
                var value = _gpio.Read(pin);
                return value == PinValue.High ? "on" : "off";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Gets the current state of all relays for heartbeat synchronization.
        /// </summary>
        public Dictionary<string, string>? GetCurrentRelayStates()
        {
            if (_gpio == null || _relayPins.Count == 0)
                return null;

            var states = new Dictionary<string, string>();

            foreach (var (relayId, pin) in _relayPins)
            {
                try
                {
                    var value = _gpio.Read(pin);
                    states[relayId] = value == PinValue.High ? "on" : "off";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GPIO] Error reading state for {relayId}: {ex.Message}");
                    states[relayId] = "off"; // Default to off on error
                }
            }

            return states;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Console.WriteLine("[GPIO] Disposing GPIO controller...");

            // Turn off all relays before cleanup
            if (_gpio != null)
            {
                foreach (var (relayId, pin) in _relayPins)
                {
                    try
                    {
                        _gpio.Write(pin, PinValue.Low);
                        Console.WriteLine($"[GPIO] Turned off {relayId} (pin {pin})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GPIO] Error turning off {relayId}: {ex.Message}");
                    }
                }

                _gpio.Dispose();
            }

            _disposed = true;
            Console.WriteLine("[GPIO] GPIO controller disposed");
        }
    }
}
