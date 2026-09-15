# Moorings

Valheim mod. Adds a **Mooring post** (Hammer → Misc, 2 Core wood + 2 Resin at a Workbench): a log bollard with rope coiled round the top.

Interact with it to tie the nearest boat (within 12m). While moored the boat:

- takes no damage from anything — wave slams, rocks, docks, rain, Ashlands
- still rides the waves, but gets pulled back if it drifts more than 3m from the post

Interact again to cast off, or just take the rudder and push forward.

Requires BepInEx + Jötunn.

## Build

```
dotnet build -c Release
```

Builds against the Steam install and copies the DLL into the r2modman `Default` profile's `BepInEx/plugins/Moorings/`.

## Support

If this saved your longship, you can [buy me a coffee on Ko-fi](https://ko-fi.com/loxleygames). More games and mods at [loxley.games](https://loxley.games).
