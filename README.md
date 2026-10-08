# RimMinder

A small plan / doing / done board for RimWorld 1.6, so you remember what you were working on between play sessions.

## Features

- **Plans board** on the bottom bar: drag tickets between Plan, Doing and Done, reorder them, and resize the board.
- **Per colony:** tickets are stored in your save.
- **Age and deadlines:** every ticket shows how many in-game days old it is; optional deadlines turn yellow, then red, with a one-time message when overdue.
- **Reminder overlay:** a small see-through list of what you're doing, draggable anywhere. Toggle it from the bottom-right icons.
- **Optional links to the game** (hidden until you open "Link to game" on a ticket):
  - *Have items:* "Have 30 meals", with a live progress bar from your stockpiles.
  - *Since event:* "Days since last raid / trader caravan / visitors…". A ticket can move back to Doing every time it happens, for recurring chores like "repair walls after a raid".
- **Settings:** hide the bottom-bar button, set a hotkey (Options > Keyboard), reset the overlay position and board size.

## What this mod does and doesn't do

- **No internet access** and no file access of its own. Tickets are saved in your save file; settings go through RimWorld's normal mod settings.
- **Two small Harmony patches**, both postfixes that only *add* behavior:
  - `PlaySettings.DoPlaySettingsGlobalControls`: adds the overlay toggle icon.
  - `IncidentWorker.TryExecute`: notes when a raid/trader/visitors event happens on your colony maps, for the "since event" timers.
- **Safe to add to an existing save.** Event timers start from the game's own history.
- **Removing it from a save** logs one harmless error on load about a missing component; your save is otherwise fine.
- Not tested with the Multiplayer mod.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Building

```
dotnet build -c Release Source/RimMinder
```

The DLL is written to `RimMinder/1.6/Assemblies/`. The `RimMinder/` folder is the mod as it's loaded by the game; `Source/` is kept outside it so it isn't uploaded to the Workshop.

## License

MIT, see [LICENSE](LICENSE).
