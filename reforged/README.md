# Inventory Reforged

**Quick stack to nearby chests that works on the current version of the game.** One key sends everything in your bag into the chests around you that already hold it. Favourite what you want to keep.

Built for the 1.0 game and kept current. Also in the box, each switchable in the config:

| | |
|---|---|
| **Stash** | Quick stack to nearby chests, hotkey or button, with favourites |
| **Craft from chests** | Workbench, forge, building, smelters, kilns, fires, cooking, fermenters all draw on nearby chests |
| **Shared chests** | Any number of players can use the same chest at the same time |
| **Armour slots** | Head / Chest / Legs / Cape / Utility in their own row, still repairable |

---

## Stash

Press **P** (or the **»** button under the weight readout on your inventory) and every item in your inventory that a nearby chest already holds goes into that chest. Chests within 10m.

Never moved: equipped gear, favourites, and the hotbar (configurable).

**Alt+click** an item to favourite it. It gets a ★ and is skipped by Stash and by the chest's own "Place stacks" button.

*How it stays safe in multiplayer:* each chest is asked through the game's own "Place stacks" request. The chest's current owner checks nobody has it open and hands it over before anything moves. Other players don't need the mod for this.

## Craft from chests

Chests within 15m count as part of your inventory when you:

- craft or upgrade at any station
- place buildings with the hammer
- add fuel to a smelter, blast furnace, kiln, fire, hearth or cooking station
- add ore, food or mead base to a smelter, cooking station or fermenter
- fire fireworks

Your own inventory is used first, then chests. Recipe counts and greyed-out entries reflect the pooled total. Chests another player currently has open are left alone.

Only the crafting, building and station code paths see the chests. Quest items, tutorial checks, ammo and anything else that asks "do you have X" still only look in your bag.

## Shared chests

Open a chest your friend already has open and you get a live view of it. You can take, put, rearrange, place stacks, take all and eat from it while they're still in it.

*How it stays safe:* the first player to open a chest owns it. Everyone after them is a guest. Every move a guest makes is sent to the owner, applied to the real chest there, and only then reflected in the guest's own inventory with exactly what moved. Two clients never write the same chest, so nothing duplicates and nothing vanishes in a race. Swaps work; the displaced item comes back to you.

The one thing a guest can't do is drop an item to the ground straight out of the chest. Take it first.

**Everyone in the world needs the mod for this feature** — the owner has to understand the guest's requests.

## Armour slots

An extra inventory row appears below your main grid with five labelled, tinted slots: **Head, Chest, Legs, Cape, Util**. Equip a piece and it moves into its slot. Unequip it and it comes back to the main grid. Drop the right piece onto a slot to equip it directly.

They are real inventory slots, so durability shows and **repair works** exactly as it always has. Weapons, shields and tools are deliberately not given slots.

The row rides on the game's own inventory-row system, so it sits after however many rows you've bought from Haldor and grows the panel the way the game does.

Removing the mod later: unequip your armour first so it's back in the main grid. Otherwise the game will drop the row's contents at your feet on your next spawn.

## Config

`BepInEx/config/games.loxley.inventoryreforged.cfg`

| Section | Setting | Default | |
|---|---|---|---|
| Stash | Key | P | |
| Stash | Range | 10 | metres |
| Stash | KeepHotbar | true | |
| Stash | PlayerBuiltOnly | true | ignore dungeon chests |
| Stash | ShowButton / ButtonX / ButtonY / ButtonSize | true / 33 / 8 / 38 | position is relative to the panel's bottom-right |
| CraftFromChests | Enabled / Range | true / 15 | |
| SharedChests | Enabled | true | |
| ArmourSlots | Enabled | true | |

## Compatibility

Replaces, and conflicts with: Quick Stack Store Sort Trash Restock, GearSlots, MultiUserChest, CraftFromContainers / AzuCraftyBoxes, and the crafting part of LazyVikings. Disable those.

Requires BepInEx. No other dependencies.

## Support

If this saved your loot, you can [buy me a coffee on Ko-fi](https://ko-fi.com/loxleygames). More games and mods at [loxley.games](https://loxley.games).
