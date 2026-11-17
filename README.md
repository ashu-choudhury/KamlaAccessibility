# Kamla Accessibility Mod

A full accessibility mod for **Kamla**, an Indian survival horror game developed by Mad-Mantra Games. 
Built for blind and visually impaired players, this mod integrates the universal **Prism** speech and braille library, providing spoken menu navigation, real-time subtitle and chant reading, crosshair interaction cues, nearby object radar, facing compass, and 3D directional audio beacons.

---

## Features

- **Universal Screen Reader & Speech Integration (Prism)**:
  - Powered by `prism.dll` (supporting NVDA, JAWS, Windows OneCore, SAPI 5, and Braille displays).
  - Built-in graceful fallback to Windows SAPI.
  - Speech interrupt and queuing support.
- **UI & Menu Accessibility**:
  - Full narration of Main Menu, Pause Menu, Difficulty Settings, Recipe Book, and Game Over / Death screen.
  - Speaks buttons, toggles (checked/not checked), sliders (percentage), dropdown options, and input fields.
  - Automatic focus keeper: pressing Arrow keys or Tab automatically focuses the first interactive button when entering a menu.
  - Procedural audio ticks and confirmation tones for crisp auditory feedback.
- **Crosshair Interaction Narration**:
  - Automatically announces interactive objects in front of the player (Doors, Cupboards, Drawers, Chests, Holy Items, Keys, Puzzle Bricks).
- **Nearby Object Radar**:
  - Press `Keypad 0` or `Alt + F` to scan within 8 meters and announce nearby items with distance in meters and clock direction (e.g. *"Matches, 2.5 meters at 1 o'clock"*).
- **Facing Compass**:
  - Press `Keypad 8` or `Alt + C` to announce facing cardinal heading (North, East, South, West) and exact yaw degrees.
- **Subtitles & Chant Reader**:
  - Automatically reads on-screen dialogues, exorcism incantations (*Om Tryambakam* chants), and story notes aloud.
- **3D Audio Beacons**:
  - Spatial audio beacon pings to guide players toward key objectives and the central courtyard altar.

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Left Control` / `Right Control` | Silence current speech immediately |
| `Keypad 5` / `Alt + R` | Repeat last spoken announcement |
| `Keypad 0` / `Alt + F` | Scan nearby items radar (distance & clock direction) |
| `Keypad 8` / `Alt + C` | Announce facing compass direction & heading |
| `F1` | Announce accessibility help & current speech backend |
| `Up / Down / Left / Right` | Navigate UI menus |
| `Tab` | Cycle forward through UI buttons |
| `Enter` / `Space` | Activate selected UI button |

---

## Technical Details

- **Engine**: Unity 2022.3.0f1 (64-bit Windows)
- **Scripting Backend**: IL2CPP (Metadata v29)
- **Mod Loader**: MelonLoader v0.6+ (.NET 6.0)
- **Speech Library**: Prism (`prism.dll`) C ABI native library
- **Target Framework**: `net6.0`

---

## Building from Source

1. Ensure the [.NET 6.0 SDK or higher](https://dotnet.microsoft.com/) is installed.
2. Clone this repository into your Git directory:
   ```cmd
   git clone https://github.com/.../KamlaAccessibility.git
   ```
3. Run `build.bat` (or `build.ps1` in PowerShell):
   ```cmd
   build.bat
   ```
4. The compiled `KamlaAccessibility.dll` is automatically placed into `E:\games\Kamla\Kamla\Mods\` and `prism.dll` is copied into the game directory.

---

## License
MIT License.
