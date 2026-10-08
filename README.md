# RimMinder

![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-informational) ![Harmony](https://img.shields.io/badge/requires-Harmony-informational) ![License: MIT](https://img.shields.io/badge/license-MIT-green)

A small plan / doing / done board for your colony, so you remember what you were working on between play sessions.

## Features

- **Plans board** on the bottom bar. Drag tickets between Plan, Doing and Done, reorder them, and resize the board.
- **Saved per colony**, inside your save file.
- **Age and deadlines.** Every ticket shows how many in-game days old it is. Optional deadlines turn yellow, then red, with a one-time message when overdue.
- **Reminder overlay.** A small see-through list of what you're doing, draggable anywhere. Toggle it from the bottom-right icons.
- **Optional links to the game** (hidden until you open "Link to game" on a ticket):
  - *Have items:* "Have 30 meals", with a live progress bar from your stockpiles.
  - *Since event:* days since the last raid, trader caravan, orbital trader or visitors. A ticket can move back to Doing every time it happens, for recurring chores like "repair walls after a raid".
- **Settings:** hide the bottom-bar button, set a hotkey, lock or reset the overlay, reset the board size.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077), loaded above RimMinder
- No DLC required; works with all of them

## Installation

- **Steam:** subscribe on the Workshop page *(link follows on release)*.
- **Manual:** download the latest [release](https://github.com/bverhage/RimMinder/releases), and unzip the `RimMinder` folder into `RimWorld/Mods`.

## Compatibility

- Safe to add to an existing save. Event timers start from the game's own history.
- Removing it from a save logs one harmless error on load about a missing component.
- Not tested with the Multiplayer mod.

## Note to translators

All text is in [`Languages/English/Keyed/RimMinder.xml`](RimMinder/Languages/English/Keyed/RimMinder.xml). Translations are welcome as pull requests or as separate language mods.

## Note to modders

RimMinder uses two Harmony postfixes:

- `RimWorld.PlaySettings.DoPlaySettingsGlobalControls`: adds the overlay toggle icon.
- `RimWorld.IncidentWorker.TryExecute`: records raids, traders and visitors on player home maps, for the "since event" timers.

## Building and testing

```
dotnet build -c Release Source/RimMinder
```

The DLL is written to `RimMinder/1.6/Assemblies/`. `RimMinder/` is the mod exactly as the game loads it; `Source/` and `Tests/` stay outside it so they're never uploaded.

`tools/test-launch.cmd` starts RimWorld with only Core, the DLCs, Harmony and RimMinder, using a separate save-data folder so your own mod list and saves aren't touched:

```
.\tools\test-launch.cmd                  # clean test colony to try things in
.\tools\test-launch.cmd -SelfTest -Quit  # automated in-game tests and screenshots
.\tools\test-launch.cmd -Screenshots "Save name"  # Steam screenshots of a save in .testdata/Saves
```

Test results go to `.testdata/SelfTest/`, Steam screenshots to `.testdata/Screenshots/`. Screenshot mode loads the save without saving over it.

## License

MIT, see [LICENSE](LICENSE).
