using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMinder
{
    public class RimMinderSettings : ModSettings
    {
        public bool showButton = true;
        public bool showOverlay = true;
        public bool overlayCollapsed;
        public bool overlayLocked;
        // Negative means "not placed yet": the overlay picks a default spot top-right.
        public Vector2 overlayPos = new Vector2(-1f, -1f);
        public Vector2 boardSize = MainTabWindow_Board.DefaultSize;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showButton, "showButton", true);
            Scribe_Values.Look(ref boardSize, "boardSize", MainTabWindow_Board.DefaultSize);
            Scribe_Values.Look(ref showOverlay, "showOverlay", true);
            Scribe_Values.Look(ref overlayCollapsed, "overlayCollapsed");
            Scribe_Values.Look(ref overlayLocked, "overlayLocked");
            Scribe_Values.Look(ref overlayPos, "overlayPos", new Vector2(-1f, -1f));
        }

        /// <summary>Shows or hides the Plans button on the bottom bar.</summary>
        public void ApplyButtonVisibility()
        {
            if (RimMinderDefOf.RimMinder_Plans != null)
                RimMinderDefOf.RimMinder_Plans.buttonVisible = showButton;
        }
    }

    public class RimMinderMod : Mod
    {
        public static RimMinderMod Instance;
        public static RimMinderSettings Settings => Instance.settings;

        private readonly RimMinderSettings settings;

        public RimMinderMod(ModContentPack content) : base(content)
        {
            Instance = this;
            settings = GetSettings<RimMinderSettings>();
            new Harmony("theB52.RimMinder").PatchAll();
        }

        public static void SaveSettings() => Instance.WriteSettings();

        public override string SettingsCategory() => "RimMinder";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect.LeftPart(0.6f));

            bool showButton = settings.showButton;
            list.CheckboxLabeled("RM_Setting_ShowButton".Translate(), ref showButton, "RM_Setting_ShowButtonTip".Translate());
            if (showButton != settings.showButton)
            {
                settings.showButton = showButton;
                settings.ApplyButtonVisibility();
            }
            if (!settings.showButton)
            {
                GUI.color = Widgets.InactiveColor;
                list.Label("RM_Setting_HiddenHint".Translate());
                GUI.color = Color.white;
            }

            list.CheckboxLabeled("RM_ShowReminder".Translate(), ref settings.showOverlay, "RM_ToggleTooltip".Translate());
            list.CheckboxLabeled("RM_Setting_LockOverlay".Translate(), ref settings.overlayLocked);

            list.Gap();
            if (list.ButtonText("RM_Setting_ResetOverlay".Translate()))
                settings.overlayPos = new Vector2(-1f, -1f);
            if (list.ButtonText("RM_Setting_ResetBoard".Translate()))
                settings.boardSize = MainTabWindow_Board.DefaultSize;

            list.Gap();
            GUI.color = Widgets.InactiveColor;
            list.Label("RM_Setting_HotkeyHint".Translate());
            GUI.color = Color.white;

            list.End();
        }
    }

    [StaticConstructorOnStartup]
    public static class RimMinderTex
    {
        public static readonly Texture2D OverlayToggle = ContentFinder<Texture2D>.Get("UI/RimMinder/OverlayToggle");

        static RimMinderTex()
        {
            // Defs are loaded by now, so this is the first safe point to apply def-based settings.
            RimMinderMod.Settings.ApplyButtonVisibility();
        }
    }

    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null)
                return;
            bool show = RimMinderMod.Settings.showOverlay;
            row.ToggleableIcon(ref show, RimMinderTex.OverlayToggle, "RM_ToggleTooltip".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            if (show != RimMinderMod.Settings.showOverlay)
            {
                RimMinderMod.Settings.showOverlay = show;
                RimMinderMod.SaveSettings();
            }
        }
    }

    [DefOf]
    public static class RimMinderDefOf
    {
        public static MainButtonDef RimMinder_Plans;
        public static KeyBindingDef RimMinder_OpenBoard;

        static RimMinderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimMinderDefOf));
        }

        public static void ToggleBoard() => Find.MainTabsRoot.ToggleTab(RimMinder_Plans);
    }
}
