# ShimejiLane

A desktop pet that walks along the bottom of your screen, climbs the walls and ceiling,
and can be picked up and thrown. Runs on Windows, macOS and Linux.

## Running it

Download the package for your system from the releases (or build it, see below):

| System | Package | How to start |
|---|---|---|
| Windows | `ShimejiLane-win-x64.zip` (`win-arm64` for ARM PCs) | Unzip, run `ShimejiLane.exe` |
| macOS, Apple Silicon (M1 and later) | `ShimejiLane-osx-arm64.zip` | Unzip, move `ShimejiLane.app` to Applications, open it |
| macOS, Intel | `ShimejiLane-osx-x64.zip` | Same as above |
| Linux | `ShimejiLane-linux-x64.tar.gz` (`linux-arm64` for ARM) | `tar xzf ShimejiLane-linux-x64.tar.gz && ./ShimejiLane/ShimejiLane` |

Quit from the tray icon (menu bar icon on macOS) or by right-clicking the pet.

**macOS:** the app isn't notarized by Apple, so the first launch is blocked. Open it once, then go to
System Settings → Privacy & Security and click **Open Anyway**. Or run
`xattr -dr com.apple.quarantine /Applications/ShimejiLane.app`.

**Linux:** needs an X11 session or XWayland (on by default in GNOME and KDE). The tray icon needs a
StatusNotifier tray. KDE has one built in; on GNOME install the *AppIndicator* extension.

## Building

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Python 3.

```
dotnet run                      # run from source on this machine
python publish.py               # build and package every platform into dist/
python publish.py osx-arm64     # or just some of them
```

Every platform can be built from any OS. The packages are self-contained, so users don't need .NET installed.
