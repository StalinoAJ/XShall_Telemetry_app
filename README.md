# SHALL Control - XR Motion Seat Telemetry Bridge

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**SHALL Control** is a high-performance telemetry bridge and motion control software designed for the SHALL XR Motion Seat. It translates real-time physics data from popular racing and simulation games into precise motion commands, providing an immersive simulation experience.

## 🚀 Key Features

- **Multi-Game Support**: Seamless integration with top-tier racing and simulation titles.
- **Dynamic Motion Processing**: Implements Butterworth, High-Pass, and Washout filters to ensure smooth, realistic seat transitions and eliminate jitter.
- **LAN-Based Control**: Communicates with the motion seat hardware via a robust HTTP CGI API over your local network.
- **Fluent UI Design**: A modern, Windows 11-inspired interface that is both beautiful and functional.
- **Auto-Discovery**: Automatically detects game installations from Steam and Xbox libraries.
- **Live Telemetry Dashboard**: Real-time visualization of G-forces, pitch, roll, and yaw data.
- **Automatic Updates**: Built-in update service to keep your software current with the latest game plugins and features.

## 🎮 Supported Games

The software currently includes dedicated plugins for:
- 🚛 **Euro Truck Simulator 2**
- 🚛 **American Truck Simulator**
- 🏎️ **Forza Horizon 5**
- 🏁 **F1 Series**
- 🏔️ **SnowRunner**
- 🏎️ **Dirt Rally**

## 🛠️ Requirements

- **OS**: Windows 10 or Windows 11 (Fluent UI optimized)
- **Runtime**: .NET Framework 4.7.2 or higher
- **Hardware**: SHALL XR Motion Seat (Connected via LAN)

## 📖 Getting Started

1. **Installation**: Download the latest release and extract the files to a folder of your choice.
2. **Network Setup**: Ensure your SHALL XR seat is powered on and connected to the same network. By default, the app looks for the seat at `192.168.1.40`.
3. **Game Configuration**:
   - Launch SHALL Control.
   - The app will attempt to detect your installed games.
   - If a game is not detected, manually set the executable path in the settings.
4. **Start Simulating**: Select your game from the dashboard and click **Start Telemetry**. Launch the game, and the seat will begin responding to in-game physics.

## ⚙️ Advanced Configuration

### Signal Filtering
The app uses a sophisticated filtering pipeline:
- **Butterworth Filter**: Smooths out high-frequency noise from telemetry data.
- **Washout Filter**: Gradually returns the seat to its neutral position during sustained G-forces, preventing the actuators from reaching their limits.

### Custom Plugins
Developers can add support for new games by implementing the `IGamePlugin` interface and placing the resulting class in the `Plugins` directory.

## 🤝 Contributing

Contributions are welcome! If you'd like to add a new game plugin or improve the motion logic:
1. Fork the repository.
2. Create a feature branch.
3. Submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
*Developed for the SHALL XR Motion Community.*
