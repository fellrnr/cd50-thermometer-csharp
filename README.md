# CD50 Thermometer - C# WPF Desktop Application

A modern C# WPF desktop application for monitoring temperatures from the inexpensive 4-channel USB CD50 thermometer.

This is a port of the Python thermometer API to C# with enhanced visualization features.

## Features

- ✅ Real-time temperature monitoring from 4 channels
- 📊 Live temperature graph with 120-point history (2 minutes at 500ms intervals)
- 🎨 Digital gauge display for each channel with color-coded readings
- 🔌 Easy COM port configuration
- ⚡ Responsive UI with threading for smooth performance
- 🎯 Connect/Disconnect/Start/Pause controls
- 📈 Automatic sensor error detection (displays "---.-°C" for invalid readings)

## Hardware

This application is compatible with the Landtek 4-Channel USB Thermometer:
- [Amazon Link](https://www.amazon.com/Landtek-Thermocouple-Thermometer-Temperature-Measurement/dp/B0C3QWBBDV)
- USB connection, no drivers needed
- 4 thermocouple input channels
- Serial protocol at 9600 baud

## System Requirements

- Windows 10 or Windows 11
- .NET 6.0 or later
- USB port for thermometer connection

## Installation

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- Visual Studio 2022 (recommended) or Visual Studio Code

### Build from Source

```bash
# Clone the repository
git clone https://github.com/fellrnr/cd50-thermometer-csharp.git
cd cd50-thermometer-csharp

# Restore dependencies
dotnet restore

# Build the application
dotnet build -c Release

# Run the application
dotnet run
```

Or simply open the solution in Visual Studio and press F5.

## Usage

1. **Connect the Device**: Plug the CD50 thermometer into a USB port on your computer
2. **Launch the Application**: Run `TemperatureMonitor.exe`
3. **Configure COM Port**: Enter the correct COM port (e.g., `COM7`) in the input field
4. **Connect**: Click the "Connect" button to establish connection with the device
5. **Start Monitoring**: Click "Start" to begin reading temperatures
6. **View Data**: 
   - Digital readings appear in the left panel for each channel
   - Real-time graph shows temperature history on the right
7. **Pause/Resume**: Use "Pause" to stop reading or "Start" again to resume
8. **Disconnect**: Click "Disconnect" when finished

## Finding Your COM Port

If you're unsure which COM port your thermometer is using:

### Windows 10/11:
1. Right-click "This PC" or "My Computer" → "Manage"
2. Click "Device Manager" in the left panel
3. Expand "Ports (COM & LPT)"
4. Look for "USB Serial Device" or similar - note the COM number

## Project Structure

```
cd50-thermometer-csharp/
├── CD50Thermometer.cs      # Serial communication driver
├── MainWindow.xaml         # UI layout (WPF)
├── MainWindow.xaml.cs      # Application logic
├── App.xaml                # Application configuration
├── App.xaml.cs             # Application code-behind
├── TemperatureMonitor.csproj # Project file with dependencies
└── README.md               # This file
```

## Technical Details

### Serial Protocol

The CD50 thermometer uses a simple binary protocol:

**Connection Handshake:**
- Host sends: `AA 55 00 03 02` (header + init command)
- Device responds: `55 AA 00 [data...] [checksum]`
- Checksum: 8-bit sum of all bytes before checksum

**Temperature Read:**
- Host sends: `AA 55 01 03 03` (header + read command)
- Device responds: 13 bytes with 4 little-endian unsigned shorts representing temperatures
- Temperature = raw_value / 10.0 (in degrees Celsius)

### Architecture

- **CD50Thermometer.cs**: Platform-independent driver for device communication
- **MainWindow**: WPF UI binding to the driver
- **Threading**: Polling loop runs on separate thread to keep UI responsive
- **Chart**: LiveCharts2 library provides the real-time graph visualization

## Dependencies

- **LiveCharts2** (v2.0.9): Chart and graphing library
- **LiveCharts2.SkiaSharp** (v2.0.9): SkiaSharp rendering backend
- **.NET Runtime**: Built-in serial port support

## Troubleshooting

### "Could not connect to [COM port]!"
- Verify the device is plugged in
- Check Device Manager for the correct COM port
- Try disconnecting and reconnecting the USB cable
- Ensure no other application is using the port

### No temperature readings after connecting
- Ensure thermometer is fully powered and connected
- Verify sensors are properly connected to the device
- Check that "Start" button has been clicked
- Invalid readings show as "---.-°C"

### Application crashes on startup
- Ensure .NET 6.0 runtime is installed: `dotnet --version`
- Try: `dotnet restore` then `dotnet run`

## License

MIT License - feel free to use this code for personal and commercial projects.

## Original Python Project

This C# version is based on the original Python implementation:
https://github.com/maszoka/cd50-thermometer

## Contributing

Contributions welcome! Feel free to submit issues or pull requests.
