using System.Device.I2c;
using System.Device.Spi;

namespace TrailerAutomationThermostatNet;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== TrailerAutomation Thermostat Client ===");
        Console.WriteLine("Loading configuration...");

        Sht31Reader? sensor = null;
        DisplayController? display = null;
        UartCommunication? uart = null;
        ThermostatController? thermostat = null;

        try
        {
            // Load configuration
            var config = ConfigurationLoader.Load();
            Console.WriteLine($"Configuration loaded successfully for device: {config.Device.ClientId}");

            // Initialize SPI for TFT display and touch
            Console.WriteLine("Initializing 2.8\" ILI9341 TFT LCD with touch...");
            var displaySpiSettings = new SpiConnectionSettings(
                config.Hardware.Display.DisplaySpiDevice, 
                config.Hardware.Display.DisplaySpiChipSelect)
            {
                ClockFrequency = 10_000_000, // 10 MHz for display
                Mode = SpiMode.Mode0
            };

            var touchSpiSettings = new SpiConnectionSettings(
                config.Hardware.Display.TouchSpiDevice, 
                config.Hardware.Display.TouchSpiChipSelect)
            {
                ClockFrequency = 2_000_000, // 2 MHz for touch
                Mode = SpiMode.Mode0
            };
            
            display = new DisplayController(
                displaySpiSettings,
                touchSpiSettings,
                dcPin: config.Hardware.Display.DataCommandPin,
                resetPin: config.Hardware.Display.ResetPin,
                backlightPin: config.Hardware.Display.BacklightPin,
                touchIrqPin: config.Hardware.Display.TouchIrqPin,
                width: config.Hardware.Display.Width,
                height: config.Hardware.Display.Height,
                timeoutSeconds: config.Hardware.Display.TimeoutSeconds
            );
            
            // Initialize I2C for temperature sensor
            Console.WriteLine("Initializing SHT31 temperature/humidity sensor...");
            sensor = new Sht31Reader(
                config.Hardware.Sensor.I2cBus, 
                config.Hardware.Sensor.I2cAddress);
            
            // Initialize UART for relay box communication
            Console.WriteLine("Initializing UART communication to relay box...");
            uart = new UartCommunication(config.Hardware.Uart);
            
            // Initialize thermostat controller
            Console.WriteLine("Initializing thermostat controller...");
            thermostat = new ThermostatController(config.Thermostat, sensor, uart, display);
            
            // Wire up touch events to thermostat
            display.TouchDetected += (sender, e) =>
            {
                Console.WriteLine($"[Touch] X={e.X}, Y={e.Y}");
                
                // Button zones (Y: 200-240)
                if (e.Y >= 200 && e.Y <= 240)
                {
                    if (e.X >= 20 && e.X <= 80)
                    {
                        // Decrease button
                        Console.WriteLine("[Touch] Decrease setpoint");
                        thermostat.DecreaseSetpoint();
                    }
                    else if (e.X >= 100 && e.X <= 160)
                    {
                        // Increase button
                        Console.WriteLine("[Touch] Increase setpoint");
                        thermostat.IncreaseSetpoint();
                    }
                    else if (e.X >= 180 && e.X <= 300)
                    {
                        // Mode button
                        Console.WriteLine("[Touch] Cycle mode");
                        thermostat.CycleMode();
                    }
                }
            };
            
            // Start all components
            Console.WriteLine("Starting all components...");
            display.Start();
            uart.Start();
            thermostat.Start();
            
            // Show initial screen
            display.DrawMessage("Thermostat Starting...");
            
            Console.WriteLine("=== Thermostat Running ===");
            Console.WriteLine("Press Ctrl+C to exit");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\nShutdown requested...");
            };

            await Task.Delay(-1, cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            Console.WriteLine("Cleaning up...");
            display?.Dispose();
            uart?.Dispose();
            sensor?.Dispose();
            Console.WriteLine("Shutdown complete.");
        }
    }
}
