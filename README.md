# Melvor Idle 2 — Early Access UI Refresh Pack v0.2.5.1

## Install on your PC

1. Export/back up your current save; close the game.
2. Extract the whole ZIP to a new folder **outside the Steam game folder**.
3. Double-click `Install-All.cmd`. It reuses your remembered game folder and still supports pasting a path or browsing to `Melvor2.exe`.
4. The script checks the exact game assembly, requires **.NET 10 SDK**, compiles the custom loader and both editable mods, then makes a timestamped backup of the working v0.2.4 components and your local saves.
5. Only after all builds succeed does it install files, refresh the modded game copy, and verify hashes.
6. Launch your original game folder's `Melvor2Modded.exe`.


The Multitasking HUD appears near the top right on a loaded character. **Drag its header** to reposition it anywhere inside the game viewport. Press **Hide/Show** to collapse/expand it. It updates every ~0.75 seconds and displays each running skill's icon and, when available, the icon of the item/activity currently being trained, crafted or gathered. Hover over a tile to see the full skill/activity name. The original mod's action logic, sidebar rings, and current-page footer remain unchanged. The HUD shows on different **in-game views**, not on top of unrelated Windows desktop applications or separate native popup windows. It keeps the dragged position for the current game process.

The Time Skip quick buttons instantly submit 1, 5, 15 or 60 minutes via the **existing game offline request**; they are disabled until a character is loaded or while a skip/offline replay is in progress. No confirmation is required for these short presets; the full menu still asks for an extra confirmation above 4 hours. Failed quick requests show the menu with the reason rather than silently doing nothing.

