# Valheim mods

One folder per mod. Each builds with `dotnet build -c Release` and deploys itself into the r2modman `Default` profile; `./build-package.sh` in a mod folder zips a Thunderstore package into `dist/`.

Paths to the Steam install and BepInEx live in `Directory.Build.props`.

| Mod | What |
|-----|------|
| [moorings](moorings/) | Mooring post — tie a boat to it and it can't be damaged |
| [reforged](reforged/) | Inventory Reforged — stash to nearby chests, shared chests, armour slots |
