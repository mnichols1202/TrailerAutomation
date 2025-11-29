# MicroPico Extension Commands (v4.3.4+)

**Extension ID:** `paulober.pico-w-go`  
**Formerly known as:** Pico-W-Go

---

## Quick Reference

All commands are accessed via `CTRL + SHIFT + P` in VS Code, then type the command name (don't type "MicroPico:" prefix).

### File Operations

| Command to Type | Full Title | Description |
|-----------------|------------|-------------|
| `Upload file` | Upload file to Pico | Upload the active file to Pico |
| `Upload project` | Upload project to Pico | Upload all project files to Pico |
| `Download file` | Download file from Pico | Download a file from Pico |
| `Download project` | Download project from Pico | Download all files from Pico |
| `Run` | Run current file on Pico | Execute the active file on Pico |
| `Delete all` | Delete all files from board | Remove all files from Pico |

### Connection

| Command to Type | Full Title | Description |
|-----------------|------------|-------------|
| `Connect` | Connect | Connect to Pico (also via status bar) |
| `Disconnect` | Disconnect | Disconnect from Pico |
| `Switch` | Switch Pico | Switch to different connected Pico |

### Reset Operations

| Command to Type | Full Title | Description |
|-----------------|------------|-------------|
| `Reset` | Reset > Soft | Soft reset MicroPython |
| `Reset` | Reset > Hard | Hard reset the Pico hardware |
| `Reset` | Reset > Hard (interactive) | Hard reset with terminal interaction |
| `Reset` | Reset > Soft (interactive) | Soft reset with terminal interaction |

### Terminal Operations

| Keyboard Shortcut | Description |
|-------------------|-------------|
| `` CTRL + ` `` | Open/close integrated terminal (vREPL) |
| `CTRL + C` | Stop running code (in terminal) |
| `CTRL + D` | Soft reset MicroPython (in terminal) |
| Command: `Stop execution` | Stop currently running code |

### File System (REPL)

Use these commands directly in the MicroPico vREPL terminal:

```python
import os

# List files
os.listdir()

# Remove a file
os.remove('filename.py')

# Create directory
os.mkdir('folder')

# Check if file exists
'main.py' in os.listdir()

# Get file size
os.stat('main.py')[6]
```

### Project Management

| Command to Type | Full Title | Description |
|-----------------|------------|-------------|
| `Initialize` | Initialize MicroPico project | Set up current folder as MicroPico project |
| `Create new` | Create new Project | Create a new MicroPico project |
| `Global settings` | Global settings | Configure global MicroPico settings |
| `Workspace settings` | Workspace settings | Configure workspace settings |

### Helpful Commands

| Command to Type | Full Title | Description |
|-----------------|------------|-------------|
| `List all` | List all Commands | Show all available MicroPico commands |
| `Pin Map` | Help > Show Pico Pin Map | Display Pico GPIO pin diagram |
| `Serial ports` | Help > List serial ports | List available COM ports |
| `Getting started` | Help > Getting started | Open help documentation |
| `Garbage collect` | Trigger garbage collection | Free memory on Pico |
| `Sync RTC` | Sync RTC | Synchronize Pico's real-time clock |

---

## Common Workflows

### Initial Upload
1. `CTRL + SHIFT + P` → Type: `Connect`
2. Select COM port from list
3. `CTRL + SHIFT + P` → Type: `Upload project`

### Edit and Test Cycle
1. Edit file in VS Code
2. `CTRL + S` (save)
3. `CTRL + SHIFT + P` → Type: `Upload file`
4. In terminal: `CTRL + D` (soft reset)
5. `>>> import filename` (test)

### Quick File Run
1. Open file in VS Code
2. `CTRL + SHIFT + P` → Type: `Run`
3. View output in terminal

---

## Installation

```powershell
code --install-extension paulober.pico-w-go
```

Or install via VS Code Extensions panel: Search for **"MicroPico"** by paulober

---

## Configuration

The extension requires a `.micropico` file in the project root:

```json
{
    "name": "TrailerAutomationClientPico",
    "sync_folder": "",
    "open_on_start": true
}
```

This file is already included in the project.

---

## Status Bar

When connected, the MicroPico status bar shows:
- **Connected:** Pico icon (clickable for quick disconnect)
- **Disconnected:** Shows available actions

Click the status bar icon to quickly connect/disconnect.

---

## Troubleshooting

### Extension Not Showing
- Verify `.micropico` file exists in project root
- Reload VS Code window (`CTRL + SHIFT + P` → Type: `Reload Window`)
- Check that extension is installed and enabled
- Try `CTRL + SHIFT + P` → Type: `List all` to see all MicroPico commands

### Upload Fails
- Verify Pico is connected (status bar shows connection)
- Check COM port is correct (`CTRL + SHIFT + P` → Type: `Serial ports`)
- Try soft reset (`CTRL + D` in terminal) then upload again
- Try hard reset (`CTRL + SHIFT + P` → Type: `Reset` → select "Hard")

### REPL Not Responding
- Press `CTRL + C` to interrupt any running code
- Press `CTRL + D` to soft reset
- Use `CTRL + SHIFT + P` → Type: `Stop execution`
- Use `CTRL + SHIFT + P` → Type: `Reset` for hard reset if needed

---

## Important Notes

### Command Palette Usage
- When you press `CTRL + SHIFT + P`, you DON'T need to type "MicroPico:" prefix
- Just type the command name: `Connect`, `Upload file`, `Run`, etc.
- The extension automatically adds "MicroPico" context to the commands

### Example
❌ Don't type: `MicroPico: Upload file to Pico`  
✅ Just type: `Upload file` (and select the one from MicroPico)

### Multiple Reset Options
When you type `Reset`, you'll see several options:
- **Reset > Soft** - Restarts MicroPython (same as `CTRL + D`)
- **Reset > Hard** - Full hardware reset (recommended for clearing errors)
- **Reset > Soft/Hard (interactive)** - Opens terminal during reset

## Differences from Pico-W-Go

MicroPico v4.3.4+ is the successor to Pico-W-Go:

### What Changed
- Renamed from Pico-W-Go to MicroPico
- Commands simplified (no "MicroPico:" prefix needed in search)
- Added new commands: WiFi management, RTC sync, garbage collection
- Improved Virtual File System support

### What Stayed the Same
- Extension ID: `paulober.pico-w-go` (unchanged for compatibility)
- `.micropico` project file format
- REPL terminal behavior (vREPL)
- Keyboard shortcuts: `CTRL + C`, `CTRL + D`, `` CTRL + ` ``

---

## See Also

- [VSCODE_SETUP.md](VSCODE_SETUP.md) - Full VS Code configuration guide
- [DEBUGGING.md](DEBUGGING.md) - Debugging strategies
- [QUICKSTART.md](QUICKSTART.md) - Quick deployment guide
