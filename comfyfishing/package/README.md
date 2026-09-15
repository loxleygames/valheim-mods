# Comfy Fishing

Fishing without the tug-of-war.

Haldor's rod is what it always was: the stamina tug-of-war. Craft a better rod and fishing changes. Cast, wait, and when something bites you get a proper moment to click (1.5s, not half a second). Then the fish reels itself in — no stamina, no escaping, no snapped line. Bigger fish take longer; higher fishing skill and a better rod bring them in faster. Skill still levels while you reel.

## Bait

Fishing bait is craftable by hand, no station: 2 Neck tails → 10 bait, so you don't need Haldor to start. The biome baits are vanilla (20 bait + a trophy at the Cauldron).

## Tiers

Bait is per biome, so bait is how the catch is gated. Each bait needs a fishing skill level and a rod tier before fish will take it:

| Bait | Skill | Rod |
|---|---|---|
| Fishing bait | 0 | Fishing rod |
| Forest | 10 | Fishing rod |
| Cave | 20 | Bone rod |
| Swamp | 25 | Bone rod |
| Ocean | 30 | Bone rod |
| Plains | 40 | Silver rod |
| Mistlands | 55 | Silver rod |
| Deep North / Ashlands | 70 | Black metal rod |

Under-levelled? It bites, you're told it's too strong for you, and it swims off. All of this is one line in the config if you'd rather tune it.

## Rods

Three new rods above Haldor's, all craftable:

| Rod | Where | Cost | Durability |
|---|---|---|---|
| Bone fishing rod | Workbench 2 | 10 Fine wood, 4 Bone fragments, 5 Resin | 100 |
| Silver fishing rod | Forge 2 | 10 Fine wood, 4 Silver, 2 Guck | 150 |
| Black metal fishing rod | Forge 3 | 10 Fine wood, 5 Black metal, 5 Linen thread | 200 |

Each tier reels 20% faster. Crafted rods **wear one point per cast** and can be **upgraded to level 4** at their station: each level adds durability and another 10% reel speed. Haldor's rod never wears, but it never gets comfy either.

## Config

`BepInEx/config/games.loxley.comfyfishing.cfg` — auto-hook (no click at all), bite window, reel speeds, and the bait tier table.

Requires BepInEx and Jötunn. Everyone fishing needs it; the server doesn't.

## Support

If this saved you a rage-quit at the pier, you can [buy me a coffee on Ko-fi](https://ko-fi.com/loxleygames). More games and mods at [loxley.games](https://loxley.games).
