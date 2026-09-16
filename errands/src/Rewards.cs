using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Errands
{
    /// What finishing an errand pays: Dvergr Marks (the token) and a biome chest whose quality is the
    /// errand's stars. Chests are Misc items opened with Use; the roll is materials, marks, sometimes a
    /// mead, sometimes the biome's chest unique.
    public static class Rewards
    {
        public const string MarkPrefab = "ErrandsMark";
        public const string ChestPrefix = "ErrandsChest_";
        public const string MarkName = "Dvergr Mark";

        static readonly int[] s_marksPerTier = { 0, 2, 4, 6 };
        static readonly float[] s_uniqueChance = { 0f, 0.08f, 0.15f, 0.30f };

        class Loot
        {
            public (string prefab, int min, int max)[] Materials;
            public string[] Meads;
        }

        static readonly Dictionary<Heightmap.Biome, Loot> s_loot = new Dictionary<Heightmap.Biome, Loot>
        {
            [Heightmap.Biome.Meadows] = new Loot
            {
                Materials = new[] { ("LeatherScraps", 8, 15), ("DeerHide", 4, 8), ("Flint", 8, 15), ("Resin", 10, 20), ("Feathers", 8, 15) },
                Meads = new[] { "MeadHealthMinor", "MeadStaminaMinor" },
            },
            [Heightmap.Biome.BlackForest] = new Loot
            {
                Materials = new[] { ("CopperOre", 6, 12), ("TinOre", 4, 8), ("FineWood", 15, 25), ("SurtlingCore", 2, 4), ("Coal", 10, 20) },
                Meads = new[] { "MeadHealthMinor", "MeadStaminaMinor", "MeadTasty" },
            },
            [Heightmap.Biome.Swamp] = new Loot
            {
                Materials = new[] { ("IronScrap", 6, 12), ("Guck", 5, 10), ("Bloodbag", 4, 8), ("Chain", 1, 2), ("Ooze", 5, 10) },
                Meads = new[] { "MeadPoisonResist", "MeadHealthMedium" },
            },
            [Heightmap.Biome.Mountain] = new Loot
            {
                Materials = new[] { ("SilverOre", 6, 12), ("Obsidian", 10, 20), ("WolfPelt", 3, 5), ("FreezeGland", 4, 8), ("Crystal", 1, 3) },
                Meads = new[] { "MeadFrostResist", "MeadHealthMedium", "MeadStaminaMedium" },
            },
            [Heightmap.Biome.Plains] = new Loot
            {
                Materials = new[] { ("BlackMetalScrap", 6, 12), ("Barley", 10, 20), ("Flax", 10, 20), ("Needle", 5, 10), ("Tar", 5, 10) },
                Meads = new[] { "MeadHealthMedium", "MeadStaminaMedium", "MeadStaminaLingering" },
            },
            [Heightmap.Biome.Mistlands] = new Loot
            {
                Materials = new[] { ("BlackMarble", 10, 20), ("Sap", 6, 12), ("Softtissue", 5, 10), ("Carapace", 5, 10), ("Eitr", 2, 4) },
                Meads = new[] { "MeadEitrMinor", "MeadHealthMajor", "MeadStaminaLingering" },
            },
            [Heightmap.Biome.AshLands] = new Loot
            {
                Materials = new[] { ("Grausten", 10, 20), ("Blackwood", 10, 20), ("FlametalOreNew", 3, 6), ("CharredBone", 8, 15) },
                Meads = new[] { "MeadHealthMajor", "MeadStaminaLingering", "MeadEitrMinor" },
            },
        };

        static readonly Dictionary<Heightmap.Biome, Color> s_chestTint = new Dictionary<Heightmap.Biome, Color>
        {
            [Heightmap.Biome.Meadows] = new Color(0.75f, 1f, 0.75f),
            [Heightmap.Biome.BlackForest] = new Color(0.65f, 0.65f, 1f),
            [Heightmap.Biome.Swamp] = new Color(0.6f, 0.8f, 0.5f),
            [Heightmap.Biome.Mountain] = new Color(0.85f, 0.95f, 1f),
            [Heightmap.Biome.Plains] = new Color(1f, 0.9f, 0.55f),
            [Heightmap.Biome.Mistlands] = new Color(0.85f, 0.65f, 1f),
            [Heightmap.Biome.AshLands] = new Color(1f, 0.55f, 0.45f),
            [Heightmap.Biome.DeepNorth] = new Color(0.9f, 0.95f, 1f),
        };

        public static string ChestPrefab(Heightmap.Biome b) => ChestPrefix + b;

        public static void AddItems()
        {
            var mark = new CustomItem(MarkPrefab, "Coins", new ItemConfig
            {
                Name = MarkName,
                Description = "A dvergr weight of silver. Haldor won't take it; the dvergr take nothing else.",
            });
            var ms = mark.ItemDrop.m_itemData.m_shared;
            ms.m_maxStackSize = 999;
            ms.m_weight = 0f;
            var silver = new Color(0.8f, 0.85f, 1f);
            Tint.Apply(mark.ItemPrefab, new Color(0.6f, 0.65f, 0.8f));
            if (ms.m_icons.Length > 0 && ms.m_icons[0]) ms.m_icons = new[] { Tint.Sprite(ms.m_icons[0], silver, desaturate: true) };
            ItemManager.Instance.AddItem(mark);

            var chestPiece = PrefabManager.Instance.GetPrefab("piece_chest_wood");
            var chestIcon = chestPiece ? chestPiece.GetComponent<Piece>()?.m_icon : null;
            foreach (var b in Biomes.All)
            {
                if (!s_loot.ContainsKey(b)) continue;
                var chest = new CustomItem(ChestPrefab(b), "Coins", new ItemConfig
                {
                    Name = $"{Biomes.Label(b)} Dvergr Chest",
                    Description = "Sealed with a dvergr's mark. Use it to see what they thought you were worth.",
                });
                var cs = chest.ItemDrop.m_itemData.m_shared;
                cs.m_maxStackSize = 20;
                cs.m_maxQuality = 3;
                cs.m_weight = 2f;
                if (chestIcon) cs.m_icons = new[] { Tint.Sprite(chestIcon, s_chestTint[b]) };
                ChestModel.Build(chest.ItemPrefab, s_chestTint[b]);
                ItemManager.Instance.AddItem(chest);
            }
        }

        public static void Grant(Player player, Errand e)
        {
            int tier = Mathf.Clamp(e.Tier, 1, 3);
            Give(player, MarkPrefab, 3 * tier, 1);
            Give(player, ChestPrefab(e.Biome), 1, tier);
        }

        /// Open a chest: roll, hand over, take the chest.
        public static void Open(Player player, ItemDrop.ItemData chest)
        {
            var biome = BiomeOf(chest);
            if (biome == Heightmap.Biome.None || !s_loot.TryGetValue(biome, out var loot)) return;
            int tier = Mathf.Clamp(chest.m_quality, 1, 3);
            player.GetInventory().RemoveItem(chest, 1);

            var got = new List<string>();
            Give(player, MarkPrefab, s_marksPerTier[tier], 1, got);

            var pool = new List<(string, int, int)>(loot.Materials);
            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                var pick = pool[Random.Range(0, pool.Count)];
                pool.Remove(pick);
                Give(player, pick.Item1, Random.Range(pick.Item2, pick.Item3 + 1), 1, got);
            }
            if (tier >= 3 && loot.Meads.Length > 0)
                Give(player, loot.Meads[Random.Range(0, loot.Meads.Length)], 1, 1, got);

            var unique = Random.value < s_uniqueChance[tier] ? Uniques.RollChestUnique(player, biome) : null;
            if (unique != null)
            {
                Give(player, unique, 1, 1, got);
                player.Message(MessageHud.MessageType.Center, $"The dvergr left you {Uniques.DisplayName(unique)}");
            }
            player.Message(MessageHud.MessageType.TopLeft, "Chest: " + string.Join(", ", got));
        }

        static Heightmap.Biome BiomeOf(ItemDrop.ItemData item)
        {
            foreach (var b in Biomes.All)
            {
                var go = ObjectDB.instance.GetItemPrefab(ChestPrefab(b));
                if (go && go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name == item.m_shared.m_name) return b;
            }
            return Heightmap.Biome.None;
        }

        /// Into the bag, or onto the ground at their feet if it won't fit.
        static void Give(Player player, string prefab, int amount, int quality, List<string> got = null)
        {
            if (amount <= 0) return;
            var go = ObjectDB.instance.GetItemPrefab(prefab);
            if (!go) { Jotunn.Logger.LogWarning($"Errands: reward prefab {prefab} missing"); return; }
            var drop = go.GetComponent<ItemDrop>();
            int stack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            int left = amount;
            while (left > 0)
            {
                int n = Mathf.Min(left, stack);
                if (player.GetInventory().AddItem(prefab, n, quality, 0, 0L, "", false) == null)
                {
                    var pos = player.transform.position + player.transform.forward * 0.5f + Vector3.up;
                    var data = drop.m_itemData.Clone();
                    data.m_quality = quality;
                    ItemDrop.DropItem(data, n, pos, Quaternion.identity);
                }
                left -= n;
            }
            got?.Add($"{amount} {Localization.instance.Localize(drop.m_itemData.m_shared.m_name)}");
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        static class UseChest
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                if (!(__instance is Player player) || player != Player.m_localPlayer || item == null) return true;
                if (BiomeOf(item) == Heightmap.Biome.None) return true;
                Open(player, item);
                return false;
            }
        }
    }

    /// A small banded box, borrowing the wooden chest's material, so a dropped chest looks like one.
    public static class ChestModel
    {
        public static void Build(GameObject prefab, Color tint)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var wood = MaterialOf("piece_chest_wood");
            var root = new GameObject("chest");
            root.transform.SetParent(prefab.transform, false);
            root.layer = prefab.layer;
            Part(root, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.24f, 0.3f), wood, tint);
            Part(root, new Vector3(0f, 0.28f, 0f), new Vector3(0.47f, 0.08f, 0.32f), wood, tint * 0.8f);
        }

        static void Part(GameObject parent, Vector3 pos, Vector3 scale, Material m, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.layer = parent.layer;
            var r = go.GetComponent<MeshRenderer>();
            if (m) { var inst = new Material(m); if (inst.HasProperty("_Color")) inst.color = tint; r.sharedMaterial = inst; }
        }

        static Material MaterialOf(string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (!prefab) return null;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m || m.shader.name.Contains("Snow") || m.name.ToLower().Contains("snow")) continue;
                    if (m.mainTexture) return m;
                }
            return null;
        }
    }

    /// Recolour a cloned prefab's materials and icon (Comfy Fishing's helpers).
    public static class Tint
    {
        public static void Apply(GameObject prefab, Color c)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_Color")) m.color = c;
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        public static Sprite Sprite(Sprite src, Color c, bool desaturate = false)
        {
            try
            {
                var rect = src.textureRect;
                var rt = RenderTexture.GetTemporary(src.texture.width, src.texture.height, 0, RenderTextureFormat.ARGB32);
                var prev = RenderTexture.active;
                Graphics.Blit(src.texture, rt);
                RenderTexture.active = rt;
                int w = (int)rect.width, h = (int)rect.height;
                var a = new Texture2D(w, h, TextureFormat.RGBA32, false);
                a.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
                var b = new Texture2D(w, h, TextureFormat.RGBA32, false);
                b.ReadPixels(new Rect(rect.x, src.texture.height - rect.y - h, w, h), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                float Alpha(Texture2D t) { float sum = 0f; foreach (var p in t.GetPixels()) sum += p.a; return sum; }
                float aa = Alpha(a), ab = Alpha(b);
                if (aa <= 0f && ab <= 0f) return src;
                var tex = aa >= ab ? a : b;
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    if (desaturate) { float g = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f; p = new Color(g, g, g, p.a); }
                    px[i] = new Color(p.r * c.r, p.g * c.g, p.b * c.b, p.a);
                }
                tex.SetPixels(px);
                tex.Apply();
                return UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
            }
            catch
            {
                return src;
            }
        }
    }
}
