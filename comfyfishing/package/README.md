# Comfy Fishing

Fishing without the tug-of-war.

Haldor's rod is what it always was: hold the button, watch your stamina drain, lose the fish. Craft your own rod and fishing changes. Cast, wait, and when something bites you get a proper moment to click (1.5s, not half a second). Then the fish reels itself in — no stamina, no escaping, no snapped line. Let go of the mouse. Bigger fish take longer; higher fishing skill and a better rod bring them in faster. Skill still levels while you reel.

## Rods

Four craftable rods, so you never need to find Haldor. Each tier unlocks more bait and reels 20% faster than the last.

| Rod | Where | Cost | Durability |
|---|---|---|---|
| Wooden fishing rod | Workbench | 10 Wood, 4 Resin, 2 Leather scraps | 60 |
| Bone fishing rod | Workbench 2 | 10 Fine wood, 4 Bone fragments, 5 Resin | 100 |
| Silver fishing rod | Forge 2 | 10 Fine wood, 4 Silver, 2 Guck | 150 |
| Black metal fishing rod | Forge 3 | 10 Fine wood, 5 Black metal, 5 Linen thread | 200 |

Crafted rods **wear one point per cast** and **upgrade to level 4** at their station — each level adds durability and another 10% reel speed. Haldor's rod never wears, but it never gets comfy either.

## Bait

Fishing bait is craftable at the Cauldron: 2 Neck tails + 1 Mushroom → 10 bait. The biome baits are vanilla (20 bait + a trophy at the Cauldron) and now each has its biome's colour so they don't all look the same.

## Tiers

Bait is per biome, so bait is how the catch is gated. Each needs a fishing skill level and a rod tier before fish will take it:

| Bait | Skill | Rod |
|---|---|---|
| Fishing bait | 0 | Wooden (or Haldor's) |
| Forest | 10 | Wooden (or Haldor's) |
| Cave | 20 | Bone |
| Swamp | 25 | Bone |
| Ocean | 30 | Bone |
| Plains | 40 | Silver |
| Mistlands | 55 | Silver |
| Deep North, Ashlands | 70 | Black metal |

Under-levelled? It bites, you're told it's too strong for you (or your rod isn't up to it), and it swims off. The whole table is one line in the config.

## Config

`BepInEx/config/games.loxley.comfyfishing.cfg`

| Setting | Default | |
|---|---|---|
| VanillaRodVanillaReel | true | Haldor's rod keeps the vanilla fight |
| AutoHook | false | hook on the bite with no click at all |
| HookWindow | 1.5 | seconds to click after a bite |
| ReelSpeed / ReelSpeedMaxSkill | 3 / 6 | metres of line per second at skill 0 / 100 |
| Tiers.Enabled | true | bait gating on/off |
| BaitTiers | (table above) | `bait:skill:rodtier`, comma separated |

Requires BepInEx and Jötunn. Everyone fishing needs it; the server doesn't.

## Coming

New fish per biome, gutting for rod-making materials, and a few more things to cook.

## Support

If this saved you a rage-quit at the pier, you can [buy me a coffee on Ko-fi](https://ko-fi.com/loxleygames). More games and mods at [loxley.games](https://loxley.games).
