# Windows Build Setup for Pico W

## Quick Start - Install Pico Setup

The easiest way to build this project on Windows is to use the official Pico setup installer:

1. **Download Pico Setup for Windows**
   - Go to: https://github.com/raspberrypi/pico-setup-windows/releases
   - Download the latest `pico-setup-windows-x64-standalone.exe`
   - Run the installer (installs to `C:\Program Files\Raspberry Pi\Pico SDK`)

2. **Build the Project**
   
   Open a **Developer Command Prompt for VS** (search in Start menu), then:
   
   ```cmd
   cd C:\Users\MichaelNichols\source\repos\TrailerAutomation\TrailerAutomationClientPicoSDK
   mkdir build
   cd build
   cmake -G "NMake Makefiles" ..
   nmake
   ```

3. **Flash to Pico W**
   - Hold BOOTSEL button while connecting Pico W via USB
   - Copy `build\TrailerAutomationClient.uf2` to the Pico drive
   - Pico will reboot and run the firmware

## Alternative: Manual SDK Setup

If you prefer to set it up manually:

1. **Install Git** (if not already installed)
   - https://git-scm.com/download/win

2. **Clone Pico SDK**
   ```powershell
   cd C:\
   git clone https://github.com/raspberrypi/pico-sdk.git
   cd pico-sdk
   git submodule update --init
   ```

3. **Set Environment Variable**
   ```powershell
   [System.Environment]::SetEnvironmentVariable("PICO_SDK_PATH", "C:\pico-sdk", "User")
   ```

4. **Install ARM GCC Toolchain**
   - Download from: https://developer.arm.com/downloads/-/gnu-rm
   - Install and add to PATH

5. **Restart VS Code** to pick up the new environment variable

6. **Build** using Developer Command Prompt as shown above

## Troubleshooting

- **"cmake is not recognized"**: Use Developer Command Prompt for VS, not regular PowerShell
- **"arm-none-eabi-gcc not found"**: Install the ARM GCC toolchain and add to PATH
- **IntelliSense errors in VS Code**: Install Pico SDK and set PICO_SDK_PATH, then reload VS Code
- **"nmake is not recognized"**: Must use Developer Command Prompt, not regular command prompt

## Using VS Code CMake Extension

Alternatively, install the CMake Tools extension in VS Code:
1. Install "CMake Tools" extension
2. Open the project folder
3. Select kit: "Visual Studio Community 2022 Release - amd64"
4. Configure and build from the VS Code status bar
