> THIS IS THE EARLY ACCESS COMPATIBILITY PACK COPY (0.2.0-ea). Build/install via the top-level Install-All.cmd.

# Melvor Idle 2 - Crafting Recipe Queue (source v0.1.0)

This is a **source build**, not a precompiled DLL. It targets the `melvor2-modloader-0.2.0` loader and the C# assembly supplied with this request. It has **not been compiled or tested in-game** in this environment, so treat it as an initial implementation.

## What it does

- Adds **+ Queue** and **Queue (N)** buttons to the stop-condition selector of compatible crafting actions (Artisan skills, Cooking and Herblore).
- Captures a recipe and its current stop configuration as a queue step, without reusing mutable settings from the UI.
- Supports the game’s existing **Recipe Creations** count and quantity/target based options: **Products Gained**, **Products Owned**, **Ingredients Spent**, and **Ingredients Remaining**, when the game offers them for that recipe.
- Starts each step through the game's own `SelectRecipe` / `StartCreating` methods.
- Advances **only** after the game reports that the current stop condition/goal was met **and** the action successfully stopped.
- Does not advance on an ordinary manual stop, lack of ingredients, or an invalid next recipe; the queue pauses instead.
- Lets you reorder/remove future steps and pause or clear the queue. Pausing/clearing does not press Stop on the current in-game activity.
- Works with same-skill recipes or compatible recipes in multiple skills. Only **one** recipe is active at a time, as in the unmodded game.

## Build and install on Windows

1. Back up `%APPDATA%\Games By Malcs\Melvor Idle 2` (your saves).
2. Install `Melvor2Modded.exe` from the mod loader's release, start it once, and close the game. The loader must have created its `modloader` directory.
3. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
4. Extract this source ZIP into an ordinary writable folder.
5. Open PowerShell in that folder, then run:

   ```powershell
   .\Build-And-Install.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Melvor Idle 2 Playtest'
   ```

   Substitute the **actual folder** containing `Melvor2.exe`.
6. Start `Melvor2Modded.exe`, not `Melvor2.exe`.

The installer copies only `Melvor2RecipeQueue.dll` and `mod.json` to `<game>\mods\Melvor2RecipeQueue\`. The game assemblies and game saves are not modified.

## Using the mod

1. Open a supported crafting skill and select the recipe you want to run **first**.
2. Expand **Manage Stop Conditions**; choose the stopping condition and configure its count, item and/or quantity.
3. Click **+ Queue** to capture the step.
4. Select the next recipe, choose its stop condition, and click **+ Queue** again. Repeat.
5. Click **Queue (N)**, review the steps in order, and press **Start Queue**.
6. The game crafts step 1 until its stop is reached, then starts step 2, etc. The last step leaves the game stopped.

If the first queued recipe is **already running with a live stop goal**, Start Queue attaches to that existing goal rather than resetting its progress. Otherwise it starts the queued step using its saved configuration. If you manually stop a recipe before its goal, the queue pauses. Start Queue again to retry it; this may restart that recipe's goal progress.

## Limits / known risks of this first version

- **Queue is in memory only.** It is lost on game exit, game restart or changing/reloading a character. It does not persist as part of the save. Do not rely on it while the game is closed.
- **Offline-progress behavior remains unverified.** The transition hooks run at the same point the game stops an action, but this version was not exercised through the actual offline simulator. It deliberately avoids Godot UI calls from background offline ticks.
- If a queued target is no longer available when its turn comes (for example, an `Owned` target is not offered under the current inventory state), the queue **pauses** rather than selecting a different stop condition.
- A second mod that allows simultaneous multiple actions could require integration changes.
- It has only been investigated against the game DLL in the provided ZIP, which may differ from your currently installed game.
- The UI uses added Godot buttons and a queue window; layout and screen scaling have not been visually QA'd.

## Troubleshooting

- Ensure **Melvor2Modded.exe** starts and the mod name is visible in its main-menu list.
- Look in `%APPDATA%\Games By Malcs\Melvor Idle 2\logs` for `[Melvor2RecipeQueue]` messages.
- If the loader says a patch failed, or the buttons are missing, capture that log and the current game version. The alpha APIs can change.
- Remove the mod by deleting `<game>\mods\Melvor2RecipeQueue`.

## Internal integration points (from the supplied assembly)

The source was based on inspection of these actual game methods:

- `GamesByMalcs.Melvor2.Components.ActionUntilX.StopOptionProviderView.Bind`
- `ActionUntilXComponent.StopConditionMetHandler`
- `ActionUntilXComponent.StopGoalIsCompleteChangedHandler`
- `ActionComponent.TryStop`
- `CraftingActionComponent<TRecipe, TActive>.SelectRecipe`
- `CraftingActionUntilXComponent<TRecipe, TActive>.StartCreating`

The patches avoid directly patching generic classes (a known loader limitation).
