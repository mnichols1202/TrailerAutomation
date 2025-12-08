using System.Device.Spi;
using System.Device.Gpio;

namespace TrailerAutomationThermostatNet;

/// <summary>
/// Controls 2.8" ILI9341 TFT LCD with XPT2046 touch (320x240, SPI).
/// Provides touch-based thermostat UI.
/// </summary>
public class DisplayController : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _dcPin;
    private readonly int _resetPin;
    private readonly int _backlightPin;
    private readonly int _timeoutSeconds;
    private readonly GpioController _gpio;
    private readonly SpiDevice _displaySpi;
    private readonly SpiDevice? _touchSpi;
    private readonly ushort[] _frameBuffer; // RGB565 framebuffer
    private readonly CancellationTokenSource _cts = new();
    private Task? _timeoutTask;
    private Task? _touchTask;
    private DateTime _lastActivity;
    private bool _isDisplayOn = true;
    private bool _disposed;

    // ILI9341 Commands
    private const byte ILI9341_SWRESET = 0x01;
    private const byte ILI9341_SLPOUT = 0x11;
    private const byte ILI9341_DISPON = 0x29;
    private const byte ILI9341_CASET = 0x2A;
    private const byte ILI9341_PASET = 0x2B;
    private const byte ILI9341_RAMWR = 0x2C;
    private const byte ILI9341_MADCTL = 0x36;
    private const byte ILI9341_PIXFMT = 0x3A;
    
    // XPT2046 Touch Commands
    private const byte XPT2046_X = 0xD0;
    private const byte XPT2046_Y = 0x90;

    // RGB565 Colors
    private const ushort ColorBlack = 0x0000;
    private const ushort ColorWhite = 0xFFFF;
    private const ushort ColorRed = 0xF800;
    private const ushort ColorGreen = 0x07E0;
    private const ushort ColorBlue = 0x001F;
    private const ushort ColorYellow = 0xFFE0;
    private const ushort ColorCyan = 0x07FF;
    private const ushort ColorMagenta = 0xF81F;

    public event EventHandler<TouchEventArgs>? TouchDetected;

    public DisplayController(
        SpiConnectionSettings displaySpiSettings,
        SpiConnectionSettings? touchSpiSettings,
        int dcPin,
        int resetPin,
        int backlightPin,
        int? touchIrqPin,
        int width,
        int height,
        int timeoutSeconds)
    {
        _width = width;
        _height = height;
        _dcPin = dcPin;
        _resetPin = resetPin;
        _backlightPin = backlightPin;
        _timeoutSeconds = timeoutSeconds;

        _gpio = new GpioController();
        _displaySpi = SpiDevice.Create(displaySpiSettings);
        
        if (touchSpiSettings != null)
        {
            _touchSpi = SpiDevice.Create(touchSpiSettings);
        }

        _frameBuffer = new ushort[width * height];

        // Setup GPIO pins
        _gpio.OpenPin(dcPin, PinMode.Output);
        _gpio.OpenPin(resetPin, PinMode.Output);
        _gpio.OpenPin(backlightPin, PinMode.Output);
        _gpio.Write(backlightPin, PinValue.High); // Backlight on

        if (touchIrqPin.HasValue)
        {
            _gpio.OpenPin(touchIrqPin.Value, PinMode.InputPullUp);
        }

        // Initialize display
        InitializeDisplay();
        _lastActivity = DateTime.UtcNow;

        Console.WriteLine($"[Display] Initialized ILI9341 {width}x{height} with touch");
    }

    private void InitializeDisplay()
    {
        // Hardware reset
        _gpio.Write(_resetPin, PinValue.Low);
        Thread.Sleep(10);
        _gpio.Write(_resetPin, PinValue.High);
        Thread.Sleep(120);

        // Software reset
        SendCommand(ILI9341_SWRESET);
        Thread.Sleep(150);

        // Exit sleep mode
        SendCommand(ILI9341_SLPOUT);
        Thread.Sleep(10);

        // Pixel format: 16-bit color
        SendCommand(ILI9341_PIXFMT);
        SendData(0x55);

        // Display orientation
        SendCommand(ILI9341_MADCTL);
        SendData(0x48); // MX, BGR

        // Display on
        SendCommand(ILI9341_DISPON);
        Thread.Sleep(10);
    }

    private void SendCommand(byte cmd)
    {
        _gpio.Write(_dcPin, PinValue.Low);
        _displaySpi.WriteByte(cmd);
    }

    private void SendData(byte data)
    {
        _gpio.Write(_dcPin, PinValue.High);
        _displaySpi.WriteByte(data);
    }

    private void SendData(byte[] data)
    {
        _gpio.Write(_dcPin, PinValue.High);
        _displaySpi.Write(data);
    }

    public void Start()
    {
        _timeoutTask = Task.Run(() => TimeoutMonitorAsync(_cts.Token), _cts.Token);
        
        if (_touchSpi != null)
        {
            _touchTask = Task.Run(() => TouchMonitorAsync(_cts.Token), _cts.Token);
        }
        
        Console.WriteLine("[Display] Started timeout and touch monitoring");
    }

    public void DrawTemperatureScreen(
        double currentTemp,
        double setpoint,
        SystemStatus status,
        ThermostatMode mode,
        double humidity)
    {
        // Clear framebuffer
        Array.Fill(_frameBuffer, ColorBlack);

        // Status color
        var statusColor = status switch
        {
            SystemStatus.Cooling => ColorBlue,
            SystemStatus.Heating => ColorRed,
            SystemStatus.Idle => ColorGreen,
            SystemStatus.WaitingCompressorDelay => ColorYellow,
            SystemStatus.Error => ColorYellow,
            _ => ColorWhite
        };

        // Draw status bar (0-40)
        FillRect(0, 0, 320, 40, statusColor);
        
        // Draw large current temperature (centered)
        var tempText = $"{currentTemp:F1}F";
        DrawTextLarge(tempText, 100, 90, ColorWhite);

        // Draw setpoint
        var setText = $"Set: {setpoint:F0}";
        DrawTextMedium(setText, 20, 170, ColorWhite);

        // Draw humidity
        var humText = $"{humidity:F0}%";
        DrawTextMedium(humText, 220, 170, ColorCyan);

        // Draw touch button outlines
        DrawRect(20, 205, 60, 30, ColorWhite);  // -
        DrawRect(100, 205, 60, 30, ColorWhite); // +
        DrawRect(180, 205, 120, 30, statusColor); // MODE

        // Send to display
        WriteFramebuffer();
    }

    public void DrawMessage(string message)
    {
        Array.Fill(_frameBuffer, ColorBlack);
        DrawTextMedium(message, 10, 100, ColorWhite);
        WriteFramebuffer();
    }

    private void FillRect(int x, int y, int w, int h, ushort color)
    {
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                if (px >= 0 && px < _width && py >= 0 && py < _height)
                {
                    _frameBuffer[py * _width + px] = color;
                }
            }
        }
    }

    private void DrawRect(int x, int y, int w, int h, ushort color)
    {
        // Top and bottom
        for (int dx = 0; dx < w; dx++)
        {
            SetPixel(x + dx, y, color);
            SetPixel(x + dx, y + h - 1, color);
        }
        // Left and right
        for (int dy = 0; dy < h; dy++)
        {
            SetPixel(x, y + dy, color);
            SetPixel(x + w - 1, y + dy, color);
        }
    }

    private void DrawTextLarge(string text, int x, int y, ushort color)
    {
        // Large text ~4x scale
        foreach (char c in text)
        {
            DrawChar(c, x, y, color, 4);
            x += 32; // 8*4
        }
    }

    private void DrawTextMedium(string text, int x, int y, ushort color)
    {
        // Medium text ~2x scale
        foreach (char c in text)
        {
            DrawChar(c, x, y, color, 2);
            x += 16; // 8*2
        }
    }

    private void DrawChar(char c, int x, int y, ushort color, int scale)
    {
        // Simple 8x8 font - implement basic characters
        byte[] charData = GetCharData(c);
        for (int row = 0; row < 8; row++)
        {
            byte rowData = charData[row];
            for (int col = 0; col < 8; col++)
            {
                if ((rowData & (1 << (7 - col))) != 0)
                {
                    // Draw scaled pixel
                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            SetPixel(x + col * scale + sx, y + row * scale + sy, color);
                        }
                    }
                }
            }
        }
    }

    private byte[] GetCharData(char c)
    {
        // Minimal font data for common characters
        return c switch
        {
            ' ' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            '0' => new byte[] { 0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00 },
            '1' => new byte[] { 0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00 },
            '2' => new byte[] { 0x3C, 0x66, 0x06, 0x0C, 0x18, 0x30, 0x7E, 0x00 },
            '3' => new byte[] { 0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00 },
            '4' => new byte[] { 0x0C, 0x1C, 0x2C, 0x4C, 0x7E, 0x0C, 0x0C, 0x00 },
            '5' => new byte[] { 0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00 },
            '6' => new byte[] { 0x3C, 0x60, 0x7C, 0x66, 0x66, 0x66, 0x3C, 0x00 },
            '7' => new byte[] { 0x7E, 0x06, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00 },
            '8' => new byte[] { 0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00 },
            '9' => new byte[] { 0x3C, 0x66, 0x66, 0x3E, 0x06, 0x0C, 0x38, 0x00 },
            '.' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00 },
            '°' => new byte[] { 0x18, 0x24, 0x24, 0x18, 0x00, 0x00, 0x00, 0x00 },
            'F' => new byte[] { 0x7E, 0x60, 0x60, 0x7C, 0x60, 0x60, 0x60, 0x00 },
            'S' => new byte[] { 0x3C, 0x60, 0x60, 0x3C, 0x06, 0x06, 0x3C, 0x00 },
            'e' => new byte[] { 0x00, 0x3C, 0x66, 0x7E, 0x60, 0x66, 0x3C, 0x00 },
            't' => new byte[] { 0x30, 0x30, 0x7C, 0x30, 0x30, 0x30, 0x1C, 0x00 },
            ':' => new byte[] { 0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00 },
            '%' => new byte[] { 0x62, 0x64, 0x08, 0x10, 0x26, 0x46, 0x00, 0x00 },
            '+' => new byte[] { 0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00 },
            '-' => new byte[] { 0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00 },
            _ => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } // Unknown char
        };
    }

    private void SetPixel(int x, int y, ushort color)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height)
        {
            _frameBuffer[y * _width + x] = color;
        }
    }

    private void WriteFramebuffer()
    {
        // Set full window
        SetWindow(0, 0, _width - 1, _height - 1);

        // Convert to byte array (big-endian RGB565)
        byte[] pixels = new byte[_frameBuffer.Length * 2];
        for (int i = 0; i < _frameBuffer.Length; i++)
        {
            pixels[i * 2] = (byte)(_frameBuffer[i] >> 8);
            pixels[i * 2 + 1] = (byte)(_frameBuffer[i] & 0xFF);
        }

        SendCommand(ILI9341_RAMWR);
        SendData(pixels);
    }

    private void SetWindow(int x0, int y0, int x1, int y1)
    {
        SendCommand(ILI9341_CASET);
        SendData((byte)(x0 >> 8));
        SendData((byte)(x0 & 0xFF));
        SendData((byte)(x1 >> 8));
        SendData((byte)(x1 & 0xFF));

        SendCommand(ILI9341_PASET);
        SendData((byte)(y0 >> 8));
        SendData((byte)(y0 & 0xFF));
        SendData((byte)(y1 >> 8));
        SendData((byte)(y1 & 0xFF));
    }

    public void RegisterActivity()
    {
        _lastActivity = DateTime.UtcNow;
        if (!_isDisplayOn)
        {
            _gpio.Write(_backlightPin, PinValue.High); // Backlight on
            _isDisplayOn = true;
        }
    }

    private async Task TimeoutMonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);
                
                var timeSinceActivity = DateTime.UtcNow - _lastActivity;
                if (timeSinceActivity.TotalSeconds > _timeoutSeconds)
                {
                    if (_isDisplayOn)
                    {
                        _gpio.Write(_backlightPin, PinValue.Low); // Backlight off
                        _isDisplayOn = false;
                        Console.WriteLine("[Display] Backlight off due to inactivity");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Display] Timeout monitor error: {ex.Message}");
            }
        }
    }

    private async Task TouchMonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(50, cancellationToken); // 20Hz polling
                
                if (_touchSpi != null)
                {
                    var (x, y, touched) = ReadTouch();
                    if (touched)
                    {
                        RegisterActivity();
                        TouchDetected?.Invoke(this, new TouchEventArgs(x, y));
                        await Task.Delay(200, cancellationToken); // Debounce
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Display] Touch monitor error: {ex.Message}");
            }
        }
    }

    private (int X, int Y, bool Touched) ReadTouch()
    {
        if (_touchSpi == null) return (0, 0, false);

        try
        {
            // Read X coordinate
            var xData = new byte[3];
            _touchSpi.TransferFullDuplex(new byte[] { XPT2046_X, 0, 0 }, xData);
            int x = ((xData[1] << 8) | xData[2]) >> 3;

            // Read Y coordinate
            var yData = new byte[3];
            _touchSpi.TransferFullDuplex(new byte[] { XPT2046_Y, 0, 0 }, yData);
            int y = ((yData[1] << 8) | yData[2]) >> 3;

            // Check if touch is valid (not 0 or max)
            bool touched = x > 200 && x < 3900 && y > 200 && y < 3900;

            if (touched)
            {
                // Map touch coordinates to screen coordinates
                x = (x - 300) * 320 / 3600;
                y = (y - 300) * 240 / 3600;
                x = Math.Clamp(x, 0, 319);
                y = Math.Clamp(y, 0, 239);
            }

            return (x, y, touched);
        }
        catch
        {
            return (0, 0, false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _cts?.Cancel();
        _timeoutTask?.Wait(TimeSpan.FromSeconds(2));
        _touchTask?.Wait(TimeSpan.FromSeconds(2));
        
        _displaySpi?.Dispose();
        _touchSpi?.Dispose();
        _gpio?.Dispose();
        _cts?.Dispose();
        
        _disposed = true;
        Console.WriteLine("[Display] Disposed");
    }
}

public class TouchEventArgs : EventArgs
{
    public int X { get; }
    public int Y { get; }

    public TouchEventArgs(int x, int y)
    {
        X = x;
        Y = y;
    }
}
