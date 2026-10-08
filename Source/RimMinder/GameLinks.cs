using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMinder
{
    public enum LinkType
    {
        None,
        Count,
        Event
    }

    public enum EventKind
    {
        Raid,
        BigThreat,
        TraderCaravan,
        OrbitalTrader,
        Visitors
    }

    /// <summary>Reads game state for linked tickets: stockpile counts and "last time X happened".</summary>
    public static class GameLinks
    {
        // Shown at the top of the "count" dropdown; anything else is under "Other item".
        private static readonly string[] CommonCategories = { "FoodMeals", "FoodRaw", "Medicine", "Textiles" };
        private static readonly string[] CommonThings = { "Silver", "Steel", "ComponentIndustrial", "WoodLog", "Plasteel", "Chemfuel", "Gold" };

        public static IEnumerable<EventKind> AllEventKinds => (EventKind[])System.Enum.GetValues(typeof(EventKind));

        public static string Label(EventKind kind) => ("RM_Event_" + kind).Translate();

        public static string Label(LinkType type) => ("RM_Link_" + type).Translate();

        public static IEnumerable<Map> HomeMaps => Find.Maps.Where(m => m.IsPlayerHome);

        public static int Count(ThingDef thing, ThingCategoryDef category)
        {
            int total = 0;
            foreach (Map map in HomeMaps)
            {
                if (thing != null)
                    total += map.resourceCounter.GetCount(thing);
                else if (category != null)
                    total += map.resourceCounter.GetCountIn(category);
            }
            return total;
        }

        public static string CountLabel(ThingDef thing, ThingCategoryDef category)
        {
            if (thing != null)
                return thing.LabelCap;
            if (category != null)
                return category.LabelCap;
            return "RM_PickItem".Translate();
        }

        /// <summary>Which of our event kinds an incident counts as.</summary>
        public static IEnumerable<EventKind> Classify(IncidentWorker worker)
        {
            if (worker is IncidentWorker_RaidEnemy)
                yield return EventKind.Raid;
            if (worker.def?.category == IncidentCategoryDefOf.ThreatBig)
                yield return EventKind.BigThreat;
            if (worker is IncidentWorker_TraderCaravanArrival)
                yield return EventKind.TraderCaravan;
            if (worker is IncidentWorker_OrbitalTraderArrival)
                yield return EventKind.OrbitalTrader;
            if (worker is IncidentWorker_VisitorGroup)
                yield return EventKind.Visitors;
        }

        /// <summary>Best guess from the game's own history, used the first time a save is loaded with this mod.</summary>
        public static int TickFromStoryState(EventKind kind)
        {
            int best = -1;
            foreach (Map map in HomeMaps)
            {
                StoryState state = map.StoryState;
                int tick = -1;
                switch (kind)
                {
                    case EventKind.Raid:
                        tick = LastFire(state, IncidentDefOf.RaidEnemy);
                        break;
                    case EventKind.BigThreat:
                        tick = state.LastThreatBigTick;
                        break;
                    case EventKind.TraderCaravan:
                        tick = LastFire(state, IncidentDefOf.TraderCaravanArrival);
                        break;
                    case EventKind.OrbitalTrader:
                        tick = LastFire(state, IncidentDefOf.OrbitalTraderArrival);
                        break;
                    case EventKind.Visitors:
                        tick = LastFire(state, IncidentDefOf.VisitorGroup);
                        break;
                }
                if (tick > best)
                    best = tick;
            }
            return best;
        }

        private static int LastFire(StoryState state, IncidentDef def)
        {
            return def != null && state.lastFireTicks != null && state.lastFireTicks.TryGetValue(def, out int tick) ? tick : -1;
        }

        /// <summary>Opens the item picker: common categories and items first, then everything by category.</summary>
        public static void OpenCountPicker(System.Action<ThingDef, ThingCategoryDef> picked)
        {
            var options = new List<FloatMenuOption>();
            foreach (string name in CommonCategories)
            {
                ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name);
                if (cat != null)
                    options.Add(new FloatMenuOption("RM_AnyOf".Translate(cat.label), () => picked(null, cat)));
            }
            foreach (string name in CommonThings)
            {
                ThingDef thing = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (thing != null)
                    options.Add(new FloatMenuOption(thing.LabelCap, () => picked(thing, null), thing));
            }
            options.Add(new FloatMenuOption("RM_OtherItem".Translate(), () => OpenCategoryPicker(picked)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void OpenCategoryPicker(System.Action<ThingDef, ThingCategoryDef> picked)
        {
            var options = DefDatabase<ThingCategoryDef>.AllDefs
                .Where(c => c.childThingDefs.Any(t => t.CountAsResource))
                .OrderBy(c => c.label)
                .Select(c => new FloatMenuOption(c.LabelCap, () => OpenThingPicker(c, picked)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void OpenThingPicker(ThingCategoryDef category, System.Action<ThingDef, ThingCategoryDef> picked)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("RM_AnyOf".Translate(category.label), () => picked(null, category))
            };
            options.AddRange(category.childThingDefs
                .Where(t => t.CountAsResource)
                .OrderBy(t => t.label)
                .Select(t => new FloatMenuOption(t.LabelCap, () => picked(t, null), t)));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_IncidentWorker
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !(parms?.target is Map map) || !map.IsPlayerHome)
                return;
            BoardComponent board = BoardComponent.Get();
            if (board == null)
                return;
            foreach (EventKind kind in GameLinks.Classify(__instance))
                board.RecordEvent(kind);
        }
    }
}
