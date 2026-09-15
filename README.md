# Valheim mods

One folder per mod. Each builds with `dotnet build -c Release` and deploys itself into the r2modman `Default` profile; `./build-package.sh` in a mod folder zips a Thunderstore package into `dist/`.

Paths to the Steam install and BepInEx live in `Directory.Build.props`.

| Mod | What |
|-----|------|
| [moorings](moorings/) | Mooring post — tie a boat to it and it can't be damaged |
| [loxleypack](loxleypack/) | LoxleyPack — modpack of all of the above |
| [comfyfishing](comfyfishing/) | Comfy Fishing — no tug-of-war, skill and rod tiers |
| [gungnir](gungnir/) | Gungnir — thrown spears return to your hand |
| [hourglass](hourglass/) | Hourglass — hold the sun at noon while you build |
| [trophypouch](trophypouch/) | Trophy Pouch — trophies tallied on the character, not the bag |
| [unburdened](unburdened/) | Unburdened — no movement penalty from armour |
| [reforged](reforged/) | Inventory Reforged — stash to nearby chests, shared chests, armour slots |
