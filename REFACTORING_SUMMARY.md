# Refactoring Summary: Unified Registry and Generalized Commands

## Overview
This refactoring unified the client/device registry system, generalized the command service, and improved TCP communication robustness.

## Changes Completed

### 1. Unified Registry System

**ClientInfo.cs** - Enhanced to include command routing information:
- Added `int CommandPort` - TCP port for sending commands
- Added `string[] Capabilities` - Device capability tags (relay, temp, humidity)
- Now serves as single source of truth for both heartbeat tracking and command routing

**ClientRegistry.cs** - Enhanced with device registration:
- Added `RegisterDevice(deviceId, ipAddress, commandPort, capabilities)` method
- Added `RemoveClient(clientId)` method for cleanup
- Now handles both heartbeat updates and command routing registration
- Single registry eliminates duplicate/stale device information

**Removed Files**:
- `DeviceRegistry.cs` - Functionality merged into ClientRegistry
- `DeviceInfo.cs` - Replaced by enhanced ClientInfo

### 2. Generalized Command System

**DeviceCommand.cs** (NEW) - Generic command model:
```csharp
public class DeviceCommand
{
    public string CommandId { get; set; }        // Unique command ID
    public string Type { get; set; }             // Command type (setRelay, etc)
    public JsonElement Payload { get; set; }     // Generic JSON payload
}

public class SetRelayPayload
{
    public string RelayId { get; set; }
    public string State { get; set; }
}
```

**DeviceCommandService.cs** - Generalized command sending:
- Now depends on `ClientRegistry` instead of `DeviceRegistry`
- New generic method: `SendCommandAsync(deviceId, DeviceCommand)`
- `SendSetRelayCommandAsync` now calls generic `SendCommandAsync` internally
- Improved timeout constants and error handling
- Uses line-delimited JSON protocol (one JSON message per line)
- Connection timeout: 5 seconds
- Response timeout: 5 seconds

**CommandResult.cs** - Standardized command response:
```csharp
public class CommandResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime TimestampUtc { get; set; }
    public object? Data { get; set; }
}
```

### 3. Improved TCP Communication (Gateway)

**Program.cs** (Gateway) - Updated all endpoints:
- Removed `DeviceRegistry` service registration
- Updated `/api/devices/register` to use `ClientRegistry.RegisterDevice()`
- Updated `/api/devices` to return clients from `ClientRegistry.GetAllClients()`
- Updated `/api/devices/{id}` to use `ClientRegistry.GetClient()`
- DeviceCommandService now injected with ClientRegistry dependency

### 4. Improved TCP Communication (Client)

**CommandListener.cs** - Refactored to async cancellation pattern:
- Changed from `Start()/Stop()` to `RunAsync(CancellationToken)`
- Removed internal CancellationTokenSource (now provided by caller)
- Better error handling with try-catch per connection
- Continues accepting connections after individual client errors
- Properly disposes resources with using statements

**Program.cs** (Client) - Improved shutdown and IP detection:
- Added CancellationTokenSource for graceful shutdown
- Added Console.CancelKeyPress handler (Ctrl+C support)
- CommandListener runs as background task with cancellation token
- All Task.Delay calls now respect cancellation token
- Improved `GetLocalIpAddress()`:
  - Now uses NetworkInterface API (better Linux compatibility)
  - Falls back to socket trick if NetworkInterface fails
  - Prioritizes non-loopback operational interfaces
  - Returns first IPv4 address found

### 5. Blazor UI Updates

**Devices.razor** - Updated to use ClientInfo:
- Internal `DeviceInfo` model now matches `ClientInfo` structure
- Added compatibility properties:
  - `DeviceId => ClientId`
  - `IpAddress => RemoteIp`
  - `LastSeen => LastHeartbeatUtc`
- CommandResult now uses shared Gateway model (no longer embedded)

## Protocol Specification

### Line-Delimited JSON
All TCP communication uses line-delimited JSON format:
- Each message is a single line of JSON
- Messages are terminated with newline (`\n`)
- StreamReader.ReadLineAsync() and StreamWriter.WriteLineAsync() handle framing

### Command Format (Gateway → Client)
```json
{
  "commandId": "guid-string",
  "type": "setRelay",
  "payload": {
    "relayId": "relay1",
    "state": "on"
  }
}
```

### Response Format (Client → Gateway)
```json
{
  "commandId": "guid-string",
  "success": true,
  "message": "Relay 'relay1' set to on",
  "errorCode": null,
  "data": {
    "relayId": "relay1",
    "state": "on",
    "pin": 17
  }
}
```

## Testing Recommendations

1. **Test Device Registration**:
   - Start Gateway
   - Start Client
   - Verify client appears in `/api/devices` endpoint
   - Verify client has CommandPort and Capabilities

2. **Test Command Sending**:
   - Use Devices.razor page to send relay commands
   - Check console logs on both Gateway and Client
   - Verify command/response JSON in logs
   - Test with offline device (should get timeout error)

3. **Test Graceful Shutdown**:
   - Start Client
   - Press Ctrl+C
   - Verify "Shutdown requested" message
   - Verify CommandListener stops cleanly

4. **Test Error Handling**:
   - Send command to non-existent device
   - Send command to device with no CommandPort
   - Send invalid relay ID
   - Verify proper error responses

## Migration Notes

### Breaking Changes
- `/api/devices` now returns `ClientInfo` objects instead of `DeviceInfo`
- Field name changes:
  - `DeviceId` → `ClientId`
  - `IpAddress` → `RemoteIp`
  - `LastSeen` → `LastHeartbeatUtc`

### Backwards Compatibility
- Devices.razor includes compatibility properties to minimize UI changes
- Heartbeat endpoint unchanged
- Sensor reading endpoints unchanged

## Future Enhancements

1. **Add More Command Types**:
   - Use generic `SendCommandAsync` to add new commands
   - Create strongly-typed payload classes like `SetRelayPayload`
   - Update CommandListener to handle new command types

2. **Command Queue**:
   - Queue commands when device is offline
   - Deliver when device comes online

3. **Command History**:
   - Store command results in database
   - Show command history in UI

4. **Command Acknowledgment**:
   - Add sequence numbers
   - Track delivery status
   - Retry failed commands

## Files Changed

### Gateway (TrailerAutomationGateway)
- ✏️ ClientInfo.cs - Added CommandPort and Capabilities
- ✏️ ClientRegistry.cs - Added RegisterDevice() and RemoveClient()
- ✏️ DeviceCommandService.cs - Refactored to generic command pattern
- ✏️ Program.cs - Updated all endpoints to use ClientRegistry
- ✏️ Components/Pages/Devices.razor - Updated model to ClientInfo
- ➕ DeviceCommand.cs - NEW generic command model
- ➕ CommandResult.cs - NEW (if didn't exist already)
- ❌ DeviceRegistry.cs - DELETED
- ❌ DeviceInfo.cs - DELETED

### Client (TrailerAutomationClientNet)
- ✏️ CommandListener.cs - Refactored to async cancellation pattern
- ✏️ Program.cs - Added cancellation support, improved IP detection

## Build Status
✅ TrailerAutomationGateway - Build succeeded  
✅ TrailerAutomationClientNet - Build succeeded  
✅ No warnings or errors
