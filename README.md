# ShimejiLane

A desktop pet that walks along the bottom of your screen, climbs the walls and ceiling,
and can be picked up and thrown. Runs on Windows, macOS and Linux.

It can also talk to [Lane](https://lane.readthedocs.io/): about once a minute it takes a screenshot,
sends it to the Lane API and shows Lane's reply in a speech bubble above the pet. This part is
optional and can be switched off at any time.

There are no prebuilt downloads. You build it yourself from source, as described below.

## What you need

| | Needed for | Get it |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Building and running (always) | Installer from the link, or your package manager |
| Python 3 | Only `publish.py` (making packages) | [python.org](https://www.python.org/downloads/) or your package manager |
| [ffmpeg](https://ffmpeg.org/download.html) | Only the Lane screenshot feature | Windows: `winget install ffmpeg` · macOS: `brew install ffmpeg` · Linux: `sudo apt install ffmpeg` (or your distro's equivalent) |
| A Lane API key | Only the Lane feature | From whoever runs your Lane server |

Check the SDK is installed with `dotnet --version` (it should print `10.x`).
ffmpeg must be on your `PATH`: `ffmpeg -version` must work in a **new** terminal.

## Run it from source

```
git clone <this repo>
cd lane_shimeji
dotnet run
```

The first build downloads the NuGet packages, so it takes a minute. After that `dotnet run` starts quickly.

## Build a standalone app

`publish.py` builds a self-contained package, so the computer that runs it doesn't need .NET installed.

```
python publish.py                 # every platform
python publish.py win-x64         # or just the ones you name
```

Use `python3` instead of `python` on macOS and Linux. Every platform can be built from any OS.
The packages end up in `dist/`:

| Target | Package | How to start |
|---|---|---|
| `win-x64`, `win-arm64` | `ShimejiLane-win-x64.zip` | Unzip, run `ShimejiLane.exe` |
| `osx-arm64` (Apple Silicon), `osx-x64` (Intel) | `ShimejiLane-osx-arm64.zip` | Unzip, move `ShimejiLane.app` to Applications, open it |
| `linux-x64`, `linux-arm64` | `ShimejiLane-linux-x64.tar.gz` | `tar xzf ShimejiLane-linux-x64.tar.gz && ./ShimejiLane/ShimejiLane` |

If you only want a build for your own machine, `dotnet publish -c Release -r <target>` also works
and puts the result in `bin/Release/net10.0/<target>/publish/`.

## Setting up Lane (optional)

1. Create a file named `.env` in the project folder (next to `ShimejiLane.csproj`):
   ```
   LANE_KEY=your-key-here
   ```
   `.env` is in `.gitignore`, so your key is never committed. The app looks for `.env` in the folder it
   was started from and then in each parent folder, so `dotnet run` from the project folder finds it.
   For a published app started from somewhere else, put `.env` next to the app, or set `LANE_KEY` as a
   normal environment variable instead. A real environment variable wins over `.env`.
2. The server address is the `LANE_URL` constant at the top of `MainWindow.axaml.cs`. Change it if you
   use a different Lane server.
3. Start the app. About 8 seconds after it connects it takes the first screenshot, then one every 60 seconds.

Without a key (or if the server can't be reached) the pet still runs normally, just without speech bubbles.

**Turning it off:** right-click the pet, or open the tray icon menu, and untick **Lane (screenshots)**.
The pet keeps walking around, and no screenshots are taken or sent until you tick it again.

**Privacy:** each screenshot of your whole screen is sent to the Lane server, and a copy is saved to a
`screens/` folder next to the executable. Lane's replies are added to `log.txt` in the same folder,
one line per reply. Neither is ever deleted by the app. Turn Lane off when you have something on screen
you don't want sent.

> **Screenshots currently only work on Windows.** They use ffmpeg's `gdigrab` capture, which only
> exists on Windows. On macOS and Linux the pet runs fine, but leave Lane turned off.

## Using it

- Drag the pet with the mouse and let go to throw it.
- Quit from the tray icon (menu bar icon on macOS) or by right-clicking the pet and choosing **Exit**.

## Platform notes

**macOS:** if you build the app on one Mac and copy the zip to another, the first launch there is
blocked because the app isn't notarized by Apple. Open it once, then go to System Settings →
Privacy & Security and click **Open Anyway**, or run
`xattr -dr com.apple.quarantine /Applications/ShimejiLane.app`.

**Linux:** needs an X11 session or XWayland (on by default in GNOME and KDE). The tray icon needs a
StatusNotifier tray. KDE has one built in; on GNOME install the *AppIndicator* extension.

## Troubleshooting

- **`LANE_KEY` not found / no bubbles ever appear:** the app couldn't find `.env`. Start it from the
  project folder, or set `LANE_KEY` as an environment variable. If you set it with `setx` or in your
  shell profile, restart your terminal (and VS Code) first.
- **Lane is on but nothing happens:** run `ffmpeg -version` in a new terminal. If it isn't found,
  ffmpeg isn't on your `PATH`.
- **Seeing errors:** the app is a GUI app, so `Console.WriteLine` output isn't shown. To get a console
  while debugging, change `<OutputType>WinExe</OutputType>` to `Exe` in `ShimejiLane.csproj`,
  and change it back afterwards.
