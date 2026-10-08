# Testing RimMinder

## 1. Automated test (about 2 minutes)

```
.\tools\test-launch.cmd -SelfTest -Quit
```

Starts RimWorld with only Core, the DLCs, Harmony and RimMinder (your own mod list and saves are not touched), runs the in-game checks and closes the game. Expect `0 failed`. Results and screenshots: `.testdata/SelfTest/`.

## 2. Manual checklist (about 15 minutes)

Start a clean test colony with `.\tools\test-launch.cmd` (dev mode is on).

**Board**
- [ ] The clipboard button on the bottom bar opens the board; clicking it again closes it.
- [ ] "+ New ticket": type a title, press Enter. It appears in Plan.
- [ ] Empty title + Save shows "Give the ticket a title first."
- [ ] Drag a ticket Plan → Doing → Done. It shows "took X d" in Done.
- [ ] Drag to reorder inside a column; the drop line shows where it lands.
- [ ] Click a ticket: editor opens. Change title, notes, color; Save keeps them, Cancel doesn't.
- [ ] Right-click a ticket: Move to…, Edit, Delete all work.
- [ ] Hover a Done ticket: the ✕ deletes it. "Clear" asks first, then empties Done.
- [ ] Resize the board with the top-right grip; right-click the grip resets it. Size is kept after closing and reopening.

**Deadlines**
- [ ] Set "Deadline in days" to 1. Card shows "due in 1.0 d".
- [ ] Dev mode: skip time (Debug actions → "Time: …" or fast-forward). Under a day it turns yellow; past it turns red, a "Plan overdue" message appears once, and the ticket shows in the overlay even if it's in Plan.

**Links to the game**
- [ ] New ticket → open "▸ Link to game" → Have items → Meals (any), 10. Card shows "Meals X / 10" with a bar. Bar updates as cooks work.
- [ ] Pick "Other item…" → a category → an item. The ticket tracks that item.
- [ ] Tick "Move to Done when reached" with a target you already have. Within an in-game hour it moves to Done with a "Goal reached" message.
- [ ] New ticket → Since event → raid, tick "Move back to Doing when it happens", move it to Done.
- [ ] Dev mode: Debug actions → Incidents → "Raid (enemy)". The ticket jumps back to Doing with a message; it shows "last raid 0.0 d ago · repeats".
- [ ] Reopening a linked ticket's editor shows the link section already open.

**Reminder overlay**
- [ ] Shows Doing tickets (and overdue Plan tickets), nothing else. Hidden when there are none.
- [ ] Drag it by the header; it stays there after restarting the game.
- [ ] Click the header (without dragging): the board opens.
- [ ] Collapse button and ✕ work. The clipboard icon in the bottom-right toggles it back on.
- [ ] Hidden on the world map.

**Settings** (Options → Mod settings → RimMinder)
- [ ] "Show the Plans button" off: the button disappears; header click still opens the board.
- [ ] Options → Keyboard → "Open colony plans": bind a key; it toggles the board.
- [ ] Reset reminder position / board size work.

**Saving**
- [ ] Save, quit to menu, load: tickets, columns, colors, deadlines and links are all intact.
- [ ] Start a new colony: the board is empty.

## 3. Your normal mod list

Load one of your own saves with RimMinder added (after Harmony). Play for a while and check `Player.log` for anything mentioning RimMinder.
