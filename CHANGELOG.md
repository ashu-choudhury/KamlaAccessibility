# Kamla Accessibility Mod changelog

## 2.0.3

- Auto-walk recalculates the route when the selected pickup moves while the player is on the way.
- Auto-walk checks the game's offering-circle trigger before announcing arrival. Offering circles have distinct names under Ritual and Puzzles.

The continuous background run entered the Haveli and placed the salt bowl and prayer plate. A later chase interrupted the route to the key book. Route recovery after some respawns, access to the statue clues, and the ending still need verification.

## 2.0.2

- Enter can recover a stale interaction lock after the player has settled inside a hiding spot and use the game's normal exit. K still reports Kamla's location while hidden.
- A newly detected auto-walk obstruction can be announced and focused even if a previous warning just played.

## 2.0.1

- F5 and automatic local refresh preserve the selected destination while it still exists in the scene. Delete can retry it after it leaves the local list.

- When a real obstacle stops auto-walk, route recovery checks the current collision geometry before switching back to the game's navigation map.
- Added a short, collision-checked way to reconnect to the navigation map when respawn or tight furniture leaves the player outside it.
- When a reachable puzzle brick blocks the route, the mod announces the brick and focuses its native action if the game allows it to be pushed.

The salt bowl return route remains unresolved. The game's navigation map returned a partial route from the recorded blocked position.

## 2.0.0

- Auto-walk checks short detours around furniture and obstructing door leaves before rejoining the planned route. Closed doors still use the game's interaction and lock rules.
- Improved checks for reaching altar, cabinet, hiding, and puzzle interaction positions. When furniture stops the final approach to an item, the mod checks whether the game already permits pickup.
- The courtyard altar reads the outstanding offerings. When the game enables the finale, the altar action changes to **Begin the final ritual**.
- Added a hotspot for the crying encounter when the game enables its trigger. The hotspot announces when the encounter starts.
- Updated the guide to distinguish implemented accessibility from playthrough coverage.

**Playthrough status.** The continuous NVDA test passed the Outer Area and placed the prayer plate. Later in the Haveli, a chase cost the test player a life and the return route to the salt bowl remained blocked. The remaining puzzles and ending still need an uninterrupted playthrough.

## 1.0.9

- Added F5 and automatic refresh for nearby objects. The list uses distance, height, and visibility, so objects seen through open doorways can appear.
- Added spoken distance and direction for the active footstep cue, strength announcements, and a **Reveal the Kalash pot** action when the game allows it.
- Added Enter to leave cover through the game's native action. The mod announces when the player can resume navigation.
- Improved pickup feedback when Kamla is chasing or the player is already carrying an item.

## 1.0.8

- Added keyboard navigation across the main and pause Options menus with Up and Down, Tab and Shift+Tab, and Enter or Space.
- Combined each pair of arrows into a setting that announces the name and current value. Left and Right adjust resolution, quality, screen mode, sliders, and VSync.
- Added separate focus and prompts for Save and Reset. The reset confirmation opens on **No, keep save files**.

## 1.0.7

- Auto-walk verifies interaction reach before announcing arrival. Enter targets the object selected for the completed route.
- Enter at the main gate uses its actual opening action and walks through. Enter at the Haveli entrance uses the game's scene transition.
- Added suitcase code entry using the number row or number pad, Enter to dial, and Backspace to correct a digit. The original wheel controls are also available.
- Replaced the combined H report with L for lives and K for Kamla's distance, direction, and elevation.
- Simplified blocked-route speech.

## 1.0.6

- Improved auto-walk across the Outer Area and through ordinary interior doors, with support for a closer approach and clearer blocked-route feedback.
- Added automatic hiding during a chase, with manual movement and Delete available to cancel the route.
- Added a directional Kamla warning sound and removed the repeating selected-hotspot chime.
- Improved object labels and actions for locks, containers, books, puzzle clues, altar circles, the handpump, and the fire.
- Placing the steel water pot now opens the handpump filling action immediately. Hold Enter to fill it.
- Added spoken suitcase wheel values and Enter confirmation, timed ritual placement feedback, and F3 to toggle automatic narration.
- Added configurable game paths for the build scripts and a build option that leaves the installed mod alone.

## 1.0.5

- Improved Outer Area terrain checks and used the game's native pickup action for the pump handle and water pot.
- Verified the main gate, both pump fills and pours, extinguishing the fire, and the transition into the Haveli in a background game copy.

## Initial release and early updates

- Added Prism screen-reader and braille support, menu navigation, subtitle and note announcements, and keyboard shortcuts.
- Added categorized hotspots, clock directions, radar, compass, auto-walk, contextual interaction menus, hiding controls, and a graphics minimizer.
- Moved navigation to the game's native player movement so footsteps and collision behavior follow the game.
- Improved item names and support for the Haveli's native doors. Replaced Windows system beeps with mod cues.
