using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyKanban
{
    public class KanbanSettings : ModSettings
    {
        public bool showOverlay = true;
        public bool overlayCollapsed;
        public bool overlayLocked;
        // Negative means "not placed yet": the overlay picks a default spot top-right.
        public Vector2 overlayPos = new Vector2(-1f, -1f);
        public Vector2 boardSize = MainTabWindow_Kanban.DefaultSize;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref boardSize, "boardSize", MainTabWindow_Kanban.DefaultSize);
            Scribe_Values.Look(ref showOverlay, "showOverlay", true);
            Scribe_Values.Look(ref overlayCollapsed, "overlayCollapsed");
            Scribe_Values.Look(ref overlayLocked, "overlayLocked");
            Scribe_Values.Look(ref overlayPos, "overlayPos", new Vector2(-1f, -1f));
        }
    }

    public class KanbanMod : Mod
    {
        public static KanbanMod Instance;
        public static KanbanSettings Settings => Instance.settings;

        private readonly KanbanSettings settings;

        public KanbanMod(ModContentPack content) : base(content)
        {
            Instance = this;
            settings = GetSettings<KanbanSettings>();
            new Harmony("billy.colonykanban").PatchAll();
        }

        public static void SaveSettings() => Instance.WriteSettings();
    }

    [StaticConstructorOnStartup]
    public static class KanbanTex
    {
        public static readonly Texture2D OverlayToggle = ContentFinder<Texture2D>.Get("UI/ColonyKanban/OverlayToggle");
    }

    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null)
                return;
            bool show = KanbanMod.Settings.showOverlay;
            row.ToggleableIcon(ref show, KanbanTex.OverlayToggle, "CK_ToggleTooltip".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            if (show != KanbanMod.Settings.showOverlay)
            {
                KanbanMod.Settings.showOverlay = show;
                KanbanMod.SaveSettings();
            }
        }
    }

    [DefOf]
    public static class KanbanDefOf
    {
        public static MainButtonDef ColonyKanban_Plans;

        static KanbanDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KanbanDefOf));
        }
    }
}
