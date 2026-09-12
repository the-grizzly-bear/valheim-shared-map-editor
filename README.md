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
  installed on the **dedicated server**, not the clients.

## Install

Drop `SharedMapEditor.dll` into the server's `BepInEx/plugins/SharedMapEditor/`. No client
install needed — this only touches world data, run from the server console.

## Use

1. `dumpmaptables` — writes `maptable-dump.txt` next to the DLL, listing
   every cartography table's pins grouped by owner ID (with a sample name/
   position per owner so you can tell which numeric ID is the player you
   want gone — their spam will stand out by count).
2. `purgemaptablepins <ownerID>` — removes every pin with that owner ID from
   every table's stored data. Prints how many were removed.

Run both from the dedicated server's own console.

## Build from source

```
dotnet build -c Release
```

Needs `-p:ValheimPath=/path/to/server` if the server isn't in the default
Steam location.
