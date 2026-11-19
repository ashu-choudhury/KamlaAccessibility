# Kamla Accessibility Mod

An accessibility mod for **Kamla**, an Indian survival horror game developed by Mad-Mantra Games.
Built for blind and visually impaired players, this mod integrates the **Prism** speech and braille library for hotspot browsing, auto-walk, contextual interactions, automatic hiding, status reports, subtitles, and graphics controls.

See [CHANGELOG.md](CHANGELOG.md) for changes by version. A continuous screen-reader-only finish through all Haveli puzzles and the ending is still unverified.

---

## Key Features

- **Universal Screen Reader & Speech Integration (Prism)**:
  - Powered by `prism.dll` (supporting NVDA, JAWS, Windows OneCore, SAPI 5, and Braille displays).
  - Built-in graceful fallback to Windows SAPI.
  - Speech interrupt and queuing support.
- **Categorized Hotspot & Auto-Walk System**:
  - `Home` / `End`: Cycle through object categories (Doors, Items, Containers, Ritual/Puzzles, Hiding Spots, Kamla, All Objects).
  - `Page Up` / `Page Down`: Cycle through individual hotspots in the selected category with real-time distance (meters), clock direction (1-12 o'clock), and state.
  - `Delete`: Walk to the selected hotspot. You can browse nearby objects even when no walking route is available.
- **Contextual Activation & Multi-Option Menus (Enter Key)**:
  - Press `Enter` to activate nearby items, open/close doors, drawers, or wardrobes.
  - When multiple interactables are nearby, pressing `Enter` opens an accessible audio menu (*"Press 1 for Key, Press 2 for Matches..."*). Press number keys `1`–`9` to choose.
- **Automatic Hiding**:
  - When Kamla starts chasing, the mod searches for a nearby hiding entrance with a walkable route and uses the game's sprint and hiding controls. Normal stamina, obstacles, and hiding conditions apply.
  - Take over with your movement controls or press `Delete` to cancel. Automatic hiding waits for the next chase after cancellation.
  - To turn it off, set `AutomaticHiding` to `false` under `[KamlaAccessibility]` in `UserData\MelonPreferences.cfg` while the game is closed.
- **Lives and Kamla Location**:
  - `L` reports remaining lives. `K` reports Kamla's distance and clock direction, including whether she's above or below you when relevant.
- **One-Key Graphics Minimizer (`F10`)**:
  - Automatically minimizes all visual quality settings to the lowest possible level (disables shadows, reflections, fog, anti-aliasing; lowers texture resolution) to ensure maximum FPS on any machine without touching audio settings.
- **Full Menu & Settings Accessibility**:
  - Keyboard navigation in Main Menu, Settings, Difficulty, Pause Menu, and Death screen.
  - Arrow keys and Tab auto-focus buttons, announce toggle states, and speak real-time slider percentages (e.g. Volume, Brightness).
  - In Options, use Up and Down or Tab and Shift+Tab to browse, Left and Right to change a setting, and Enter or Space to activate a button or toggle. Save and Reset confirmations have their own keyboard focus.
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
| `Up` / `Down`, then `Enter` | Browse and activate any nearby action when the interaction menu is open |
| `1` – `9` | Choose one of the first nine actions directly |
| `L` | Announce remaining lives |
| `K` | Announce Kamla's distance, clock direction, and whether she is above or below you |
| `F3` | Toggle automatic subtitle, prompt, and crosshair narration |
| `F5` | Refresh nearby objects in the current category and repeat any active footstep cue |
| `F10` | **Minimize Graphic Settings** for maximum performance |
| `Keypad 0` / `Alt + F` | Scan nearby radar (distance & clock direction) |
| `Keypad 8` / `Alt + C` | Announce facing compass heading |
| `Left Control` / `Right Control` | Silence speech immediately |
| `Keypad 5` / `Alt + R` | Repeat last spoken announcement |
| `F1` | Announce keyboard shortcuts and speech status |
| `Up / Down / Left / Right` | Navigate UI menus / adjust sliders |
| `Tab` | Cycle forward through UI buttons |

---

## Walking and puzzles

- Page Up and Page Down announce the selected hotspot's distance and clock direction. Kamla's warning sound pans as you turn and pulses faster as she approaches or chases you. Change `EnableKamlaBeacon` and `BeaconVolume` in `UserData\MelonPreferences.cfg` while the game is closed.
- Delete announces the first direction of the route when auto-walk starts. Routes look for an approach near the interaction area and can check nearby detours around obstacles.
- When movement is obstructed, the mod announces the obstacle or missing footing. After auto-walk announces arrival, Enter uses the selected object. At the main gate, Enter opens the gate and walks through it. At the Haveli entrance, Enter uses the game's scene entrance.
- Browse **Ritual and Puzzles** to find nearby puzzle objects and revealed clues. In the suitcase, type the combination on the number row or number pad, then press Enter to dial it. Backspace corrects a digit. Left and Right still select a wheel, Up and Down turn it, and Escape leaves. Hold Enter when prompted for the handpump or ritual placement. The footstep puzzle announces the game's visible clue meter.
- In the Outer Area, the **Handpump** and **Fire** hotspots report the puzzle state. After placing the **Steel water pot** at the pump, hold Enter to fill it. Use the pump action again to pick up the filled pot. After the first pour, refill it for the second.
- At the **Courtyard ritual altar**, choose **Check ritual progress** to hear the remaining offerings. The action changes to **Begin the final ritual** when the game makes it available.
- While carrying the smudge stick, press Enter and choose **Follow the footstep trail** to try auto-walk. F5 repeats the cue's direction and distance for manual movement. Choose **Reveal the Kalash pot** when within reach.
- F3 affects automatic narration. Menus, explicit inspection, puzzle control feedback, and obstacle warnings remain available.

Kamla's warning sound is adapted from `bong_001.ogg` in Kenney's [Interface Sounds](https://opengameart.org/content/interface-sounds), released under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).

## Building from Source

1. Install the [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) and [MelonLoader for Windows x64](https://github.com/LavaGang/MelonLoader/releases).
2. Launch Kamla once with MelonLoader to generate the game assemblies. Close the game before deploying the mod.
3. Run `build.bat` (or `build.ps1` in PowerShell):
   ```cmd
   build.bat
   ```
4. The build deploys `KamlaAccessibility.dll` to `D:\game\Kamla\Mods\` and `prism.dll` to the game folder by default. For a different installation, pass its path to `build.bat` or use `build.ps1 -GameDir <path>`. To compile without deploying, run `dotnet build -c Release -p:DeployMod=false`.

---

## License
MIT License.
