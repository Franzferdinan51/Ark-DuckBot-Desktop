# ArkDuckBot Desktop

ArkDuckBot Desktop is a Windows WPF companion app for ARK: Survival Ascended servers. It provides server pairing, live tracking, map and device controls, camera viewing, AI chat integration, and tray-based background operation.

## What it does

- Connects to an ARK server through the DuckBot ServerAPI / tracking stack
- Pairs with your account through the `arkduckbot://` protocol or manual setup
- Shows live server state, players, devices, and map activity
- Provides a built-in AI chat panel backed by the DuckBot MCP bridge
- Includes camera viewing, mini map, crosshair tools, hotkeys, and notification controls
- Runs in the system tray and supports single-instance startup
- Checks for app updates from inside the client

## Main UI Areas

The current app surface includes:

- **Pairing / Login** - Steam-based pairing flow and protocol-link handling
- **Map** - live map view, markers, tracking, and related controls
- **Devices** - smart devices, switches, alarms, and grouped controls
- **Cameras** - camera list and frame viewing
- **AI** - DuckBot chat and assistant tooling
- **Settings** - connection, notifications, and app preferences
- **Utility controls** - mini map, crosshair, hotkeys, patch notes, and server info

## Requirements

- Windows
- .NET 8 SDK for building from source
- ARK: Survival Ascended server with the DuckBot / ServerAPI backend configured
- DuckBot MCP bridge running for AI chat features

## Build and Run

```bash
git clone https://github.com/Franzferdinan51/Ark-DuckBot-Desktop.git
cd Ark-DuckBot-Desktop/ArkDuckBotDesktop
dotnet restore
dotnet build
dotnet run
```

To publish a release build:

```bash
dotnet publish -c Release --self-contained
```

## First Run

1. Launch the app.
2. Open **Settings** and configure the ARK server connection and notifications.
3. Start the pairing flow if the account is not already linked.
4. Open the map, devices, cameras, or AI tab once the server connection is active.

## Configuration

The app stores user configuration under:

```text
%APPDATA%\ArkDuckBot\
```

Common settings include server host and port, MCP bridge URL and secret, notification preferences, tray behavior, and per-server hotkeys.

## Notes

- The app is single-instance.
- Opening an `arkduckbot://` link routes into the running instance when possible.
- Closing the window can minimize to tray, depending on the configured behavior.
- The window title currently uses the `RUST+ DESKTOP` branding in the UI.

## Related Projects

- [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark) - ARK ServerAPI plugin and MCP bridge backend
- [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI) - WindowsGSM integration for ARK SA with ServerAPI support
- [rustplus-desktop](https://github.com/Pronwan/rustplus-desktop) - original companion-app inspiration

## License

GPL-3.0 License - see [LICENSE](LICENSE)

## Author

- Franz Ferdinand - <https://github.com/Franzferdinan51>

