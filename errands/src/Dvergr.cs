using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Errands
{
    /// Per-instance dressing for a placed dvergr: which biome he belongs to (stored on the ZDO so every
    /// client agrees), his name, his lines, and a tint on his clothes. Also finds himself a clear patch of
    /// floor the first time he loads, since placement drops him at the ruin's centre, chest or no chest.
    public class Dvergr : MonoBehaviour
    {
        public const string BiomeKey = "errands_biome";
        public const string SettledKey = "errands_settled";
        public const string IdKey = "errands_id";

        private ZNetView m_nview;
        private Trader m_trader;
        private bool m_settled;
        public Heightmap.Biome Biome { get; private set; }
        /// Persistent per-dvergr identity: seeds his name and his daily offers. Set at placement; a
        /// console-spawned one draws his own.
        public int Id { get; private set; }
        public string Name => m_trader ? m_trader.m_name : "Dvergr";
        public List<Shop.Shelf> Shelves { get; private set; }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_trader = GetComponent<Trader>();
        }

        private void Start()
        {
            if (!m_nview || !m_nview.IsValid()) return;
            Biome = (Heightmap.Biome)m_nview.GetZDO().GetInt(BiomeKey);
            if (Biome == Heightmap.Biome.None)
            {
                // Spawned by hand (console) rather than by placement: take the biome we're standing in.
                Biome = WorldGenerator.instance.GetBiome(transform.position);
                if (m_nview.IsOwner()) m_nview.GetZDO().Set(BiomeKey, (int)Biome);
            }
            Id = m_nview.GetZDO().GetInt(IdKey);
            if (Id == 0)
            {
                // Placed before ids existed, or spawned by hand: draw one and keep it.
                Id = Random.Range(1, int.MaxValue);
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(IdKey, Id);
            }
            m_settled = m_nview.GetZDO().GetBool(SettledKey);
            Dress();
        }

        private void Update()
        {
            if (m_settled || !m_nview || !m_nview.IsValid()) return;
            // Whoever loads him first moves him. Ownership is needed to write the ZDO; nobody else has
            // claimed a freshly generated ZDO yet, so taking it is uncontested.
            m_nview.ClaimOwnership();
            Settle();
            m_settled = true;
            m_nview.GetZDO().Set(SettledKey, true);
        }

        public static void SetBiome(GameObject go, Heightmap.Biome biome, int id)
        {
            var nview = go.GetComponent<ZNetView>();
            if (nview && nview.GetZDO() != null)
            {
                nview.GetZDO().Set(BiomeKey, (int)biome);
                nview.GetZDO().Set(IdKey, id);
            }
        }

        /// Nearest clear spot to where he was dropped: a ray finds the floor, a capsule his height checks
        /// nothing solid stands there. Spirals outward; the ruin centre wins if it's free.
        private void Settle()
        {
            var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain");
            var origin = transform.position;
            var own = GetComponentsInChildren<Collider>(true);
            foreach (var c in own) c.enabled = false;
            try
            {
                foreach (var candidate in Candidates(origin))
                {
                    if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out var hit, 6f, mask)) continue;
                    if (Mathf.Abs(hit.point.y - origin.y) > 2.5f) continue; // roof or cellar
                    var feet = hit.point + Vector3.up * 0.3f;
                    if (Physics.CheckCapsule(feet, feet + Vector3.up * 1.4f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue;
                    transform.position = hit.point;
                    m_nview.GetZDO().SetPosition(hit.point);
                    return;
                }
            }
            finally
            {
                foreach (var c in own) c.enabled = true;
            }
        }

        private static IEnumerable<Vector3> Candidates(Vector3 origin)
        {
            yield return origin;
            for (float r = 1.2f; r <= 5f; r += 1.2f)
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    yield return origin + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
        }

        private void Dress()
        {
            m_trader.m_name = Biomes.DvergrName(Id);
            Lines.Fill(m_trader, Biome);
            Shelves = Shop.Stock(m_trader, Biome);
            Tinting.Apply(gameObject, Biome);
        }

        /// Hold E: his wares, priced in marks.
        public void OpenWares()
        {
            if (StoreGui.instance == null) return;
            Popup.CloseAll();
            if (StoreGui.IsVisible()) return;
            Say(Lines.Wares);
            StoreGui.instance.Show(m_trader);
        }

        /// A tap is decided on release: let go quickly and he talks errands, keep holding and he shows
        /// his wares. Decided before any window opens, so nothing blocks the input in between.
        public void TapOrHold()
        {
            if (m_deciding) return;
            StartCoroutine(Decide());
        }

        bool m_deciding;

        System.Collections.IEnumerator Decide()
        {
            m_deciding = true;
            float t = 0f;
            while (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
            {
                t += Time.deltaTime;
                if (t >= 0.35f) { m_deciding = false; OpenWares(); yield break; }
                yield return null;
            }
            m_deciding = false;
            Interact();
        }

        /// Talking to him: he takes any finished errands of his, then offers today's.
        public void Interact()
        {
            var finished = Journal.Active().FindAll(e => e.Biome == Biome && Journal.Complete(e));
            foreach (var e in finished) Journal.HandIn(e, Biome);

            var offer = Journal.TodaysOffer(this);
            if (offer == null) Say(Lines.NoErrands);
            else if (Journal.Taken(Id, offer.Day)) Say(finished.Count > 0 ? Lines.HandIn : Lines.Tomorrow);
            else if (Journal.Full) Say(Lines.HandsFull);
            else
            {
                Say(finished.Count > 0 ? Lines.HandIn : Lines.Offer);
                OfferDialog.Show(this, offer);
            }
        }

        public void Say(List<string> lines)
        {
            Chat.instance.SetNpcText(gameObject, Vector3.up * m_trader.m_dialogHeight, 20f, m_trader.m_hideDialogDelay, "",
                Lines.Pick(lines), large: false);
            var anim = GetComponentInChildren<Animator>();
            if (anim) anim.SetTrigger("Talk");
        }
    }

    [HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
    static class Trader_Interact_Patch
    {
        static bool Prefix(Trader __instance, bool hold, ref bool __result)
        {
            var dvergr = __instance.GetComponent<Dvergr>();
            if (!dvergr) return true;
            __result = false;
            if (!hold) dvergr.TapOrHold();
            return false;
        }
    }

    /// What the dvergr say. Plain strings (no `$` tokens) pass straight through Localize.
    public static class Lines
    {
        public static readonly List<string> Greet = new List<string>
        {
            "Hm. Another one of Odin's strays.",
            "Mind the roof. It was better before the trolls.",
            "You're tall. They're all tall.",
            "Sit if you must. Don't touch anything.",
        };

        public static readonly List<string> Talk = new List<string>
        {
            "The old ones built this. We only keep it standing.",
            "There's work, if you want it. There's always work.",
            "Ten thousand years and the roof still leaks.",
            "The forest remembers everything. So do I.",
            "Don't ask about the Mistlands.",
        };

        public static readonly List<string> Goodbye = new List<string>
        {
            "Mind the roof.",
            "Don't die. The paperwork is endless.",
            "Come back when you've done something useful.",
        };

        public static readonly List<string> NoErrands = new List<string>
        {
            "Nothing for you here. Nothing for anyone here.",
            "No work. Just weather.",
        };

        public static readonly List<string> Offer = new List<string>
        {
            "There's work, if you want it.",
            "I need a thing done. You look like you have hands.",
            "Odin's strays are good for one thing. Fetching.",
        };

        public static readonly List<string> HandIn = new List<string>
        {
            "Hm. Not bad. For a tall one.",
            "Good. Now I owe you, and I hate that.",
            "That's that, then. Sit. Don't touch anything.",
        };

        public static readonly List<string> Tomorrow = new List<string>
        {
            "You have my errand. Find another of us, or come back tomorrow.",
            "One a day from me. The others may be softer.",
        };

        public static readonly List<string> Wares = new List<string>
        {
            "Marks only. I don't want your coins.",
            "Look, don't touch. Then pay, then touch.",
            "Kin get the good shelf. You're not kin.",
        };

        public static readonly List<string> HandsFull = new List<string>
        {
            "Your hands are full. Finish something.",
            "Five errands and you want a sixth? Go away.",
        };

        public static string Pick(List<string> list) => list[Random.Range(0, list.Count)];

        public static void Fill(Trader t, Heightmap.Biome biome)
        {
            t.m_randomGreets = Greet;
            t.m_randomTalk = Talk;
            t.m_randomGoodbye = Goodbye;
            t.m_randomStartTrade = NoErrands;
            t.m_randomBuy = Talk;
            t.m_randomSell = Talk;
            t.m_randomGiveItemNo = Talk;
            t.m_randomUseItemAlreadyRecieved = Talk;
        }
    }
}
