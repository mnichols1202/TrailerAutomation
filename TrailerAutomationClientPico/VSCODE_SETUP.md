# VS Code Setup for Pico 2 W Development

Complete guide to setting up Visual Studio Code for MicroPython development on the Pico 2 W.

---

## Quick Setup (5 minutes)

### Prerequisites
✅ `.micropico` file exists in project root (already created!)
✅ VS Code installed
✅ Pico 2 W connected via USB

### 1. Install Required Extensions

Open VS Code and install these extensions:

**Essential:**
- **MicroPico** by paulober (v4.3.4+, formerly Pico-W-Go - Best for Pico 2 W!)
- **Pylance** (Python language support)
- **Python** by Microsoft

**Optional but Recommended:**
- **Error Lens** (inline error display)
- **Better Comments** (color-coded comments)
- **GitLens** (if using Git)

### Install Extensions via Command Line

```powershell
# Open PowerShell and run:
code --install-extension paulober.pico-w-go
code --install-extension ms-python.python
code --install-extension ms-python.vscode-pylance
code --install-extension usernamehw.errorlens
code --install-extension aaron-bond.better-comments
```

---

## Step-by-Step VS Code Configuration

### Step 1: Open Your Project

```powershell
cd "c:\Users\MichaelNichols\source\repos\TrailerAutomation\TrailerAutomationClientPico"
code .
```

### Step 2: Configure MicroPico Extension

**IMPORTANT:** The MicroPico extension needs a `.micropico` file in your project root to recognize it as a Pico project.

This file has already been created for you! It tells VS Code this is a MicroPico project.

If you ever need to create it manually:
1. Create a file named `.micropico` (no extension) in project root
2. Add this content:
   ```json
   {
       "name": "TrailerAutomationClientPico",
       "sync_folder": "",
       "open_on_start": true
   }
   ```
3. Reload VS Code: `CTRL + SHIFT + P` → "Reload Window"

### Step 3: Connect to Pico

**Method A: Status Bar (Bottom)**
- Look for the MicroPico icon in the status bar (bottom)
- Click it to connect/disconnect
- Select your COM port when prompted
- Status shows connection state

**Method B: Command Palette**
1. `CTRL + SHIFT + P`
2. Type: `Connect` (MicroPico)
3. Select COM port from list

### Step 4: Open Terminal/REPL

- **View → Terminal** or `` CTRL + ` ``
- You'll see MicroPython REPL with `>>>`
- Type Python commands directly!

---

## Key Features & Shortcuts

### File Operations

**Note:** MicroPico extension (v4.3.4+, formerly Pico-W-Go) commands:

| Action | Command Palette | Description |
|--------|----------------|-------------|
| **Upload Current File** | `MicroPico: Upload current file to Pico` | Upload active file to Pico |
| **Upload Project** | `MicroPico: Upload project to Pico` | Upload all files |
| **Run Current File** | `MicroPico: Run current file on Pico` | Execute file on Pico |
| **Delete File** | `MicroPico: Delete file from Pico` | Remove file |
| **Reset Pico** | `MicroPico: Reset` | Hard reset device |
### Debugging Operations

| Action | Shortcut | Command |
|--------|----------|---------|
| **Open Terminal** | `` CTRL + ` `` | Interactive REPL (vREPL) |
| **Stop Execution** | `CTRL + C` (in terminal) | Stop running code |
| **Soft Reset** | `CTRL + D` (in terminal) | Restart MicroPython |
| **Hard Reset** | `CTRL + SHIFT + P` → `Reset > Hard` | Reset hardware |
| **Connect** | Status bar or `Connect` | Connect to Pico |
| **Disconnect** | Status bar or `Disconnect` | Disconnect from Pico |
| **Soft Reset** | `CTRL + D` | Restart MicroPython |
| **Hard Reset** | `CTRL + SHIFT + P` → "Reset" | Reset hardware |

### REPL Operations

| Action | Keys | Description |
|--------|------|-------------|
| **Interrupt** | `CTRL + C` | Stop current code |
| **Soft Reboot** | `CTRL + D` | Restart interpreter |
| **Paste Mode** | `CTRL + E` | Multi-line paste |
| **Clear Screen** | Type `clear()` | Clear output |

---

## Workspace Configuration Files

I'll create the necessary VS Code configuration files for you:

### `.vscode/settings.json`
```json
{
    "python.languageServer": "Pylance",
    "python.analysis.typeCheckingMode": "basic",
    "python.analysis.extraPaths": [
        "${workspaceFolder}"
    ],
    "micropico.syncFolder": "",
    "micropico.openOnStart": true,
    "files.autoSave": "afterDelay",
    "files.autoSaveDelay": 1000,
    "editor.formatOnSave": false,
    "python.linting.enabled": false,
    "[python]": {
        "editor.tabSize": 4,
        "editor.insertSpaces": true,
        "editor.wordBasedSuggestions": false
    },
    "files.exclude": {
        "**/__pycache__": true,
        "**/*.pyc": true
    }
}
```

### `.vscode/extensions.json`
```json
{
    "recommendations": [
        "paulober.pico-w-go",
        "ms-python.python",
        "ms-python.vscode-pylance",
        "usernamehw.errorlens",
        "aaron-bond.better-comments"
    ]
}
```

---

## Debugging Workflow in VS Code

### Basic Debugging Session

1. **Connect to Pico**
   - Click status bar: "Pico Disconnected" → Select COM port
   - Status shows: "Pico (COM3)"

2. **Open Terminal**
   - `` CTRL + ` ``
   - You see `>>>`

3. **Test Individual Components**
   ```python
   >>> import test_wifi
   >>> import test_i2c_scan
   >>> import test_sht31
   ```

4. **Run Main Program**
   ```python
   >>> import main
   # Watch output in real-time
   ```

5. **Stop if Needed**
   - `CTRL + C` in terminal

6. **Make Changes**
   - Edit files in VS Code
   - Auto-saved (or `CTRL + S`)

7. **Upload Changes**
   - `CTRL + SHIFT + P` → "Upload file to Pico"
   - Or upload entire project with "Upload project to Pico"

8. **Test Again**
   - `CTRL + D` (soft reset)
   - `>>> import main`

### Interactive Debugging

```python
# In terminal (>>>)

# 1. Import and test config
>>> from config import Config
>>> config = Config()
>>> print(config.WIFI_SSID)
MM0001

# 2. Test WiFi
>>> import network
>>> wlan = network.WLAN(network.STA_IF)
>>> wlan.active(True)
>>> wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
>>> # Wait...
>>> wlan.isconnected()
True
>>> wlan.ifconfig()
('192.168.1.50', '255.255.255.0', '192.168.1.1', '192.168.1.1')

# 3. Test sensor
>>> from sht31_reader import Sht31Reader
>>> sensor = Sht31Reader()
>>> temp, humid = sensor.read_measurement()
>>> print(f"Temp: {temp:.2f}°C, Humidity: {humid:.2f}%")

# 4. Test gateway discovery
>>> from gateway_discovery import discover_gateway
>>> gateway = discover_gateway()
>>> print(gateway)

# 5. Test full flow
>>> import main
# Watch it run...
```

### Side-by-Side Debugging

**Layout Setup:**
1. **Left Side:** Your Python files (editor)
2. **Right Side:** Split with:
   - Top: Output/Problems panel
   - Bottom: Integrated Terminal (REPL)

**How to Split:**
1. Open a file (e.g., `main.py`)
2. `CTRL + SHIFT + P` → "View: Split Editor Right"
3. Open `config.py` on right side
4. `` CTRL + ` `` for terminal at bottom
5. `CTRL + J` to toggle panel visibility

---

## Advanced VS Code Features

### 1. Multi-Cursor Editing

```python
# Add print statements to multiple lines at once:
# 1. Click first line
# 2. CTRL + ALT + Down Arrow (add cursor below)
# 3. Type: print(f"[DEBUG] 
# All lines edited simultaneously!
```

### 2. Code Snippets

Create `.vscode/python.code-snippets`:
```json
{
    "Debug Print": {
        "prefix": "dbg",
        "body": [
            "print(f\"[DEBUG] ${1:variable}: {${1:variable}}\")"
        ],
        "description": "Debug print statement"
    },
    "Try Except": {
        "prefix": "tryex",
        "body": [
            "try:",
            "    ${1:pass}",
            "except Exception as e:",
            "    print(f\"Error: {e}\")",
            "    import sys",
            "    sys.print_exception(e)"
        ],
        "description": "Try-except with traceback"
    }
}
```

Usage: Type `dbg` then TAB → instant debug print!

### 3. Tasks for Common Operations

Create `.vscode/tasks.json`:
```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "Upload All Files",
            "type": "shell",
            "command": "echo",
            "args": ["Use CTRL+SHIFT+P -> Upload project to Pico"],
            "problemMatcher": [],
            "group": "build"
        },
        {
            "label": "Soft Reset Pico",
            "type": "shell",
            "command": "echo",
            "args": ["Press CTRL+D in terminal"],
            "problemMatcher": []
        }
    ]
}
```

### 4. File Watchers

Automatically upload on save:
```json
// In .vscode/settings.json
{
    "micropico.autoConnect": true,
    "micropico.syncFolder": "",
    "files.autoSave": "afterDelay"
}
```

---

## Troubleshooting VS Code Setup

### Issue: Extension Not Connecting

**Check COM Port:**
```powershell
# In PowerShell
Get-WmiObject Win32_SerialPort | Select-Object DeviceID, Name
```

**Reconnect:**
1. Unplug Pico
2. Close VS Code
3. Plug in Pico
4. Open VS Code
5. `CTRL + SHIFT + P` → "MicroPico: Connect"

### Issue: REPL Not Responding

**Solution:**
1. Click in terminal
2. Press `CTRL + C` (stop execution)
3. Press `CTRL + D` (soft reboot)
4. You should see startup message and `>>>`

### Issue: Upload Fails

**Solution:**
1. Make sure Pico is connected (check status bar)
2. Close any other programs using the COM port
3. Try disconnecting and reconnecting
4. Check file isn't open in another editor

### Issue: IntelliSense Not Working

**Solution:**
1. Install Pylance extension
2. `CTRL + SHIFT + P` → "Python: Select Interpreter"
3. Choose any Python 3.x
4. Reload window: `CTRL + SHIFT + P` → "Reload Window"

---

## Keyboard Shortcuts Cheat Sheet

### File Operations
- `CTRL + N` - New file
- `CTRL + O` - Open file
- `CTRL + S` - Save file
- `CTRL + W` - Close file
- `CTRL + SHIFT + S` - Save all

### Navigation
- `CTRL + P` - Quick open file
- `CTRL + SHIFT + P` - Command palette
- `CTRL + TAB` - Switch between files
- `CTRL + B` - Toggle sidebar

### Editing
- `CTRL + /` - Toggle comment
- `CTRL + D` - Select next occurrence
- `ALT + Up/Down` - Move line up/down
- `SHIFT + ALT + Up/Down` - Copy line up/down
- `CTRL + SHIFT + K` - Delete line

### Terminal
- `` CTRL + ` `` - Toggle terminal
- `CTRL + SHIFT + 5` - Split terminal
- `CTRL + C` - Stop execution (in terminal)
- `CTRL + D` - Soft reset (in terminal)

### MicroPico Specific
- `CTRL + SHIFT + P` → Type command:
  - "Upload file" - Upload current file to Pico
  - "Upload project" - Upload all files to Pico
  - "Run" - Run current file on Pico
  - "Connect" - Connect to Pico
  - "Reset" - Reset Pico (Soft or Hard)

---

## Recommended VS Code Layout

```
┌─────────────────────────────────────────────────────────────┐
│ File  Edit  View  ...                    [Pico (COM3)] ●   │
├──────────┬──────────────────────────────────────────────────┤
│          │  main.py                                         │
│ Explorer │  ─────────────────────────────────────────      │
│          │  1  import time                                  │
│ • main.py│  2  import json                                  │
│ • config │  3  from config import Config                    │
│ • sht31_ │  4                                               │
│ • gatewa │  5  config = Config()                            │
│ • test_  │  6  print(config.WIFI_SSID)                      │
│          │                                                   │
├──────────┼───────────────────────────────────────────────── │
│ TERMINAL │  >>> import main                                 │
│          │  TrailerAutomationClientPico starting...         │
│ > _      │  Connecting to WiFi: MM0001                      │
│          │  Connected! IP: 192.168.1.50                     │
│          │  Gateway discovered: http://192.168.1.100:5000   │
│          │  [22:45:10] Heartbeat OK: {"status":"ok"}        │
│          │  [22:45:15] Sensor reading: TempC=22.45 ...      │
│          │  >>>                                              │
└──────────┴───────────────────────────────────────────────────┘
```

**To Achieve This:**
1. `CTRL + B` (show sidebar)
2. `` CTRL + ` `` (show terminal)
3. Drag terminal divider to size
4. `CTRL + K, CTRL + S` (keyboard shortcuts)

---

## Pro Tips for VS Code + Pico

### 1. Use Multi-File Search
`CTRL + SHIFT + F` - Search across all files
- Great for finding where functions are defined
- Search for `TODO` comments

### 2. Use Git Integration
- Track your changes
- Commit working versions
- Easy rollback if something breaks

### 3. Use Zen Mode for Focus
`CTRL + K, Z` - Distraction-free coding
- ESC to exit

### 4. Use Breadcrumbs
View → Show Breadcrumbs
- See current function/class at top

### 5. Use Outline View
View → Outline
- Navigate large files easily

### 6. Use Problems Panel
`CTRL + SHIFT + M` - See all syntax errors
- Pylance catches issues before upload

### 7. Use Live Share (Collaborative Debugging!)
Install: Live Share extension
- Share your VS Code session
- Debug together in real-time
- Great for remote help

---

## Testing Workflow in VS Code

### 1. Open Project
```powershell
cd TrailerAutomationClientPico
code .
```

### 2. Connect Pico
- Status bar: Click "Pico Disconnected"
- Select COM port

### 3. Open Files
- `main.py` (left)
- `` CTRL + ` `` (terminal bottom)

### 4. Run Tests
```python
>>> import test_wifi
>>> import test_i2c_scan
>>> import test_sht31
>>> import test_discovery
```

### 5. Run Main
```python
>>> import main
# Watch output
```

### 6. Debug Issues
```python
>>> # Sensor not working?
>>> import machine
>>> i2c = machine.I2C(0, sda=machine.Pin(0), scl=machine.Pin(1))
>>> i2c.scan()
[68]  # 68 = 0x44 in decimal (SHT31 found!)
```

### 7. Fix and Upload
- Edit file
- `CTRL + S` (save)
- `CTRL + SHIFT + P` → "Upload file to Pico"
- `CTRL + D` (reset in terminal)
- Test again

---

## Remote Development (Bonus!)

### Using Remote - SSH Extension

**If Pico is connected to another computer:**

1. Install "Remote - SSH" extension
2. Connect to remote machine:
   ```
   CTRL + SHIFT + P → "Remote-SSH: Connect to Host"
   ```
3. Enter: `user@192.168.1.100`
4. Open folder on remote machine
5. Install MicroPico on remote VS Code
6. Debug remotely!

---

## Summary: Your Complete Setup

### Extensions to Install:
```powershell
code --install-extension paulober.pico-w-go
code --install-extension ms-python.python
code --install-extension ms-python.vscode-pylance
code --install-extension usernamehw.errorlens
```

### Quick Start:
1. Open project: `code .`
2. Connect Pico: Click status bar
3. Open terminal: `` CTRL + ` ``
4. Test code: `>>> import main`
5. Edit → Save → Upload → Test

### Most Used Commands:
- `CTRL + SHIFT + P` - Command palette (do everything!)
- `` CTRL + ` `` - Toggle terminal
- `CTRL + C` - Stop execution
- `CTRL + D` - Soft reset

You're all set for collaborative debugging in VS Code! 🎯
