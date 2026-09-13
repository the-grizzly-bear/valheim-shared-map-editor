# Shared Map Editor

Server-side BepInEx mod that removes one specific player's pins from every
cartography table's shared map data — without wiping the exploration
progress or pins belonging to anyone else.

Vanilla's `resetsharedmap` only clears every pin that isn't yours on the
client running it, and it comes back: cartography table shared-map data is
stored per-table on the world (a ZDO byte blob), so the next time anyone
reads from any table, the old data resyncs right back in. This mod edits
that stored blob directly and permanently.

## Requirements

- [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
  installed on the **dedicated server**.

## Install

Drop `SharedMapEditor.dll` into the server's `BepInEx/plugins/SharedMapEditor/`.
Only the server needs it — unless you want to run the commands from an
admin's in-game console instead of the server's own terminal, in which case
that admin's client needs it installed too (see below). Nobody else needs
it either way.

## Use

1. `dumpmaptables` — writes `maptable-dump.txt` next to the DLL, listing
   every cartography table's pins grouped by owner ID. Each group also shows
   `author='<steamid>'`.
2. **Find which owner ID is which player:** each player's Steam ID64 is on
   their own Steam profile page (profile → Copy Page URL — if the URL is a
   custom name instead of numbers, paste it into steamid.io to get the
   number). Match that number against the `author=` field in the dump; the
   `ownerID` on that same line is that player's ID. No local file digging
   needed — everyone's ID is already in the dump.
3. The spammer's group will also usually just have way more pins than
   anyone else's, so you often don't even need to match IDs at all.
4. `purgemaptablepins <ownerID>` — removes every pin with that owner ID from
   every table's stored data. Prints how many were removed.

Run both commands from the dedicated server's own console. They also work
from an admin's in-game console (`F5` → `devcommands` first) if that admin's
client also has `SharedMapEditor.dll` installed — the command gets relayed
to the server automatically, same as `kick`/`ban`, but only if that player
is in the server's `adminlist.txt`.

## Build from source

```
dotnet build -c Release
```

Needs `-p:ValheimPath=/path/to/server` if the server isn't in the default
Steam location.
