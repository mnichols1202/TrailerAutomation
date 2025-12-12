using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// Manages physical buttons for relay control (local and remote)
    /// Uses System.Device.Gpio for GPIO access
    /// Implements same logic as web UI - tracks state locally and sends explicit commands
    /// </summary>
    public class ButtonController : IDisposable
    {
        private readonly AppConfiguration _config;
        private readonly GpioController _gpioController;
        private readonly GpioRelayController _relayController;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<int, ButtonState> _buttonStates = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _monitorTask;
        
        private const int DebounceDelayMs = 50;
        private const int PollingIntervalMs = 20; // 20ms polling = 50Hz, good balance between responsiveness and CPU usage
        
        private class ButtonState
        {
            public ButtonConfig Config { get; set; } = null!;
            public PinValue LastValue { get; set; }
            public PinValue LastStableValue { get; set; }
            public DateTime LastDebounceTime { get; set; }
            public bool RelayState { get; set; } // Track relay state locally like web UI
        }
        
        public ButtonController(AppConfiguration config, GpioRelayController relayController, HttpClient httpClient)
        {
            _config = config;
            _relayController = relayController;
            _httpClient = httpClient;
            _gpioController = new GpioController();
            
            InitializeButtons();
        }
        
        private void InitializeButtons()
        {
            var enabledButtons = _config.Hardware.Buttons
                .Where(b => b.Enabled)
                .ToList();
            
            if (enabledButtons.Count == 0)
            {
                Console.WriteLine("[Button] No buttons configured");
                return;
            }
            
            Console.WriteLine($"[Button] Initializing {enabledButtons.Count} button(s)...");
            
            foreach (var button in enabledButtons)
            {
                try
                {
                    // Configure pin as input with pull-up (active LOW)
                    _gpioController.OpenPin(button.Pin, PinMode.InputPullUp);
                    
                    // Read actual relay state instead of assuming false
                    var actualState = false;
                    if (string.IsNullOrEmpty(button.TargetDevice) || button.TargetDevice == _config.Device.ClientId)
                    {
                        // Local relay - read actual state
                        var stateStr = _relayController?.GetRelayState(button.TargetRelay);
                        actualState = stateStr == "on";
                    }
                    
                    var state = new ButtonState
                    {
                        Config = button,
                        LastValue = PinValue.High, // Pull-up = High when not pressed
                        LastStableValue = PinValue.High,
                        LastDebounceTime = DateTime.UtcNow,
                        RelayState = actualState // Use actual relay state
                    };
                    
                    _buttonStates[button.Pin] = state;
                    
                    Console.WriteLine($"[Button] [{button.Id}] {button.Name} initialized on pin {button.Pin} -> {button.TargetDevice}:{button.TargetRelay}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Button] ERROR: Failed to initialize button {button.Id}: {ex.Message}");
                }
            }
        }
        
        public void StartMonitoring()
        {
            if (_buttonStates.Count == 0)
            {
                Console.WriteLine("[Button] No buttons to monitor");
                return;
            }
            
            _monitorTask = Task.Run(() => MonitorButtonsAsync(_cts.Token), _cts.Token);
            Console.WriteLine("[Button] Monitoring started");
        }
        
        /// <summary>
        /// Update button state tracking when relay state changes externally (e.g., from web UI)
        /// </summary>
        public void SyncRelayState(string relayId, string state)
        {
            var newState = state == "on";
            
            foreach (var btnState in _buttonStates.Values)
            {
                if (btnState.Config.TargetRelay == relayId)
                {
                    btnState.RelayState = newState;
                    Console.WriteLine($"[Button] Synced button '{btnState.Config.Name}' relay state to {state}");
                }
            }
        }
        
        private async Task MonitorButtonsAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Button] Monitor thread started");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Poll every 20ms (50Hz) for good responsiveness without excessive CPU usage
                    await Task.Delay(PollingIntervalMs, cancellationToken);
                    
                    foreach (var kvp in _buttonStates)
                    {
                        var pin = kvp.Key;
                        var state = kvp.Value;
                        
                        // Read current button state (active LOW with pull-up)
                        var currentValue = _gpioController.Read(pin);
                        
                        // Check if state changed (reset debounce timer)
                        if (currentValue != state.LastValue)
                        {
                            Console.WriteLine($"[Button] Pin {pin} state changed: {state.LastValue} -> {currentValue}");
                            state.LastDebounceTime = DateTime.UtcNow;
                            state.LastValue = currentValue;
                        }
                        
                        // If debounce time has passed, the reading is stable
                        if ((DateTime.UtcNow - state.LastDebounceTime).TotalMilliseconds > DebounceDelayMs)
                        {
                            // Check for rising edge (Low -> High transition after debounce)
                            if (state.LastStableValue == PinValue.Low && currentValue == PinValue.High)
                            {
                                // Button was pressed and now released - toggle!
                                Console.WriteLine($"[Button] Button '{state.Config.Name}' pressed");
                                
                                // Check if target is local or remote
                                bool isLocal = string.IsNullOrEmpty(state.Config.TargetDevice) ||
                                             state.Config.TargetDevice == _config.Device.ClientId;
                                
                                Console.WriteLine($"[Button] TargetDevice: '{state.Config.TargetDevice}', ClientId: '{_config.Device.ClientId}', IsLocal: {isLocal}");
                                Console.WriteLine($"[Button] TargetRelay: '{state.Config.TargetRelay}'");
                                
                                if (isLocal)
                                {
                                    await HandleLocalToggleAsync(state);
                                }
                                else
                                {
                                    await HandleRemoteToggleAsync(state);
                                }
                            }
                            
                            // Update stable value after debounce period
                            state.LastStableValue = currentValue;
                        }
                    }
                    
                    // Small delay to avoid excessive CPU usage
                    await Task.Delay(10, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[Button] Monitor error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        
        private async Task HandleLocalToggleAsync(ButtonState btnState)
        {
            // Flip local state tracking (like web UI does)
            btnState.RelayState = !btnState.RelayState;
            var stateStr = btnState.RelayState ? "on" : "off";
            
            try
            {
                if (_relayController.SetRelay(btnState.Config.TargetRelay, stateStr))
                {
                    Console.WriteLine($"[Button] Set local relay '{btnState.Config.TargetRelay}' to {stateStr.ToUpper()}");
                    
                    // Notify gateway of state change so web UI updates
                    await NotifyGatewayOfRelayStateAsync(btnState.Config.TargetRelay, stateStr);
                }
                else
                {
                    Console.WriteLine($"[Button] ERROR: Failed to set relay state for '{btnState.Config.TargetRelay}'");
                    // Revert state on failure
                    btnState.RelayState = !btnState.RelayState;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Button] ERROR: Failed to set relay state: {ex.Message}");
                // Revert state on failure
                btnState.RelayState = !btnState.RelayState;
            }
        }
        
        private async Task HandleRemoteToggleAsync(ButtonState btnState)
        {
            // Send toggle command to gateway which will forward to remote device
            try
            {
                var url = $"/api/devices/{btnState.Config.TargetDevice}/relays/{btnState.Config.TargetRelay}/toggle";
                
                Console.WriteLine($"[Button] Toggling remote relay {btnState.Config.TargetDevice}:{btnState.Config.TargetRelay}");
                
                var response = await _httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                
                Console.WriteLine($"[Button] Remote relay toggled successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Button] ERROR: Remote toggle failed: {ex.Message}");
            }
        }
        
        private async Task NotifyGatewayOfRelayStateAsync(string relayId, string state)
        {
            try
            {
                // Send state change notification to gateway so web UI updates (best-effort, non-blocking)
                var url = $"/api/devices/{_config.Device.ClientId}/relays/{relayId}/state?state={state}";
                
                Console.WriteLine($"[Button] Notifying gateway (best-effort)");
                
                // Use short timeout for responsive button feel
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                var response = await _httpClient.PostAsync(url, null, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Button] Gateway notified successfully");
                }
                else
                {
                    Console.WriteLine($"[Button] Gateway notification failed (offline?) - relay still activated");
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[Button] Gateway notification timeout - relay still activated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Button] Gateway offline - relay activated locally only: {ex.Message}");
                // Don't throw - local relay operation succeeded, gateway notification is best-effort
            }
        }
        
        public void Dispose()
        {
            _cts?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(2));
            
            foreach (var pin in _buttonStates.Keys)
            {
                try
                {
                    _gpioController.ClosePin(pin);
                }
                catch { /* Ignore disposal errors */ }
            }
            
            _gpioController?.Dispose();
            _cts?.Dispose();
        }
    }
}
