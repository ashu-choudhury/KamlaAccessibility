# Kamla Accessibility Mod

A full accessibility mod for **Kamla**, an Indian survival horror game developed by Mad-Mantra Games. 
Built specifically for blind and visually impaired players, this mod integrates the universal **Prism** speech and braille library, providing complete mouse-free gameplay: categorized hotspot browsing, auto-walk navigation, contextual interaction menus, emergency hiding shortcuts, health/threat status reports, real-time subtitle reading, and one-key graphics minimization.

---

## Key Features

- **Universal Screen Reader & Speech Integration (Prism)**:
  - Powered by `prism.dll` (supporting NVDA, JAWS, Windows OneCore, SAPI 5, and Braille displays).
  - Built-in graceful fallback to Windows SAPI.
  - Speech interrupt and queuing support.
- **Categorized Hotspot & Auto-Walk System**:
  - `Home` / `End`: Cycle through object categories (Doors, Items, Containers, Ritual/Puzzles, Hiding Spots, Kamla, All Objects).
  - `Page Up` / `Page Down`: Cycle through individual hotspots in the selected category with real-time distance (meters), clock direction (1-12 o'clock), and state.
  - `Delete`: **Auto-walk to selected hotspot**! Characters automatically walk towards the target with destination guidance and stop upon arrival.
- **Contextual Activation & Multi-Option Menus (Enter Key)**:
  - Press `Enter` to activate nearby items, open/close doors, drawers, or wardrobes.
  - When multiple interactables are nearby, pressing `Enter` opens an accessible audio menu (*"Press 1 for Key, Press 2 for Matches..."*). Press number keys `1`–`9` to choose.
- **Emergency Hiding Shortcut (`Alt + H`)**:
  - Instantly locates the nearest hiding spot (wardrobe, almirah, bed, or safe zone) and safely conceals the player.
  - Announces the hiding location and status.
- **Health & Player Status (`H` Key)**:
  - Reports remaining lives, stamina percentage, carried items, current room/area, and Kamla's active threat status (distance, clock direction, and whether roaming or chasing).
- **One-Key Graphics Minimizer (`F10`)**:
  - Automatically minimizes all visual quality settings to the lowest possible level (disables shadows, reflections, fog, anti-aliasing; lowers texture resolution) to ensure maximum FPS on any machine without touching audio settings.
- **Full Menu & Settings Accessibility**:
  - 100% mouse-free navigation in Main Menu, Settings, Difficulty, Pause Menu, and Death screen.
  - Arrow keys and Tab auto-focus buttons, announce toggle states, and speak real-time slider percentages (e.g. Volume, Brightness).
- **Subtitles & Mantra Reader**:
  - Real-time narration of on-screen dialogue, exorcism mantras (*Om Tryambakam* chants), and story notes.

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Home` / `End` | Cycle Hotspot category (Doors, Items, Containers, Puzzles, Hiding, Kamla, All) |
| `Page Up` / `Page Down` | Browse objects in current category (Distance, Clock direction, State) |
| `Delete` | **Auto-walk** to the selected hotspot (WASD or Delete again to stop) |
| `Enter` / `Keypad Enter` | Interact with object, pick up item, or open multi-option action menu |
| `1` – `9` | Choose option in multi-option action menu |
| `Alt + H` | **Emergency Hide**: automatically find and enter nearest hiding spot |
| `H` | Announce health, lives, stamina, location, and Kamla threat status |
| `F10` | **Minimize Graphic Settings** for maximum performance |
| `Keypad 0` / `Alt + F` | Scan nearby radar (distance & clock direction) |
| `Keypad 8` / `Alt + C` | Announce facing compass heading |
| `Left Control` / `Right Control` | Silence speech immediately |
| `Keypad 5` / `Alt + R` | Repeat last spoken announcement |
| `F1` | Announce keyboard shortcuts and speech status |
| `Up / Down / Left / Right` | Navigate UI menus / adjust sliders |
| `Tab` | Cycle forward through UI buttons |

---

## Building from Source

1. Ensure the [.NET 6.0 SDK or higher](https://dotnet.microsoft.com/) is installed.
2. Run `build.bat` (or `build.ps1` in PowerShell):
   ```cmd
   build.bat
   ```
3. `KamlaAccessibility.dll` is automatically compiled and copied to `E:\games\Kamla\Kamla\Mods\`.

---

## License
MIT License.
