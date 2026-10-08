using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMinder
{
    /// <summary>The small see-through list of what you're working on, drawn over the map.</summary>
    [StaticConstructorOnStartup]
    public static class ReminderOverlay
    {
        private const int WindowId = 0x4B414E42; // "KANB"
        private const float Width = 210f;
        private const float HeaderHeight = 20f;
        private const float Pad = 6f;
        private const float RowGap = 4f;
        private const float Stripe = 2f;
        private const float MetaHeight = 15f;
        private const float IconSize = 14f;

        private static readonly Color Background = new Color(0.08f, 0.1f, 0.11f, 0.55f);
        private static readonly Color HeaderText = new Color(0.78f, 0.78f, 0.78f);

        private static bool dragging;
        private static Vector2 dragOffset;
        private static Vector2 pressStart;
        private const float ClickSlop = 4f;

        private static RimMinderSettings Settings => RimMinderMod.Settings;

        public static void OnGUI(BoardComponent board)
        {
            if (!Settings.showOverlay || Find.CurrentMap == null || WorldRendererUtility.WorldRendered)
                return;
            if (Find.UIRoot?.screenshotMode != null && Find.UIRoot.screenshotMode.FiltersCurrentEvent)
                return;

            List<Ticket> items = board.tickets
                .Where(t => t.status == TicketStatus.Doing || (t.status == TicketStatus.Plan && t.IsOverdue))
                .OrderBy(t => t.status == TicketStatus.Doing ? 0 : 1)
                .ToList();
            if (items.Count == 0)
            {
                dragging = false;
                return;
            }

            float textWidth = Width - Pad * 2f - Stripe - 5f;
            float height = HeaderHeight;
            if (!Settings.overlayCollapsed)
            {
                height += 2f;
                foreach (Ticket t in items)
                    height += RowHeight(t, textWidth) + RowGap;
                height += Pad - RowGap;
            }

            HandleDrag();
            Rect rect = new Rect(Position(height), new Vector2(Width, height));

            Find.WindowStack.ImmediateWindow(WindowId, rect, WindowLayer.GameUI, () => DoContents(rect.AtZero(), items, textWidth),
                doBackground: false, absorbInputAroundWindow: false, shadowAlpha: 0f);
        }

        private static float RowHeight(Ticket t, float textWidth)
        {
            Text.Font = GameFont.Tiny;
            return Text.CalcHeight(t.title, textWidth) + MetaHeight;
        }

        private static Vector2 Position(float height)
        {
            Vector2 pos = Settings.overlayPos;
            if (pos.x < 0f || pos.y < 0f)
                pos = new Vector2(UI.screenWidth - Width - 12f, 270f);
            pos.x = Mathf.Clamp(pos.x, 0f, UI.screenWidth - Width);
            pos.y = Mathf.Clamp(pos.y, 0f, UI.screenHeight - height - 35f);
            return pos;
        }

        private static void HandleDrag()
        {
            if (!dragging)
                return;
            if (!Input.GetMouseButton(0))
            {
                dragging = false;
                // Barely moved: treat it as a click on the header and open the board.
                if ((UI.MousePositionOnUIInverted - pressStart).magnitude < ClickSlop)
                    RimMinderDefOf.ToggleBoard();
                else
                    RimMinderMod.SaveSettings();
                return;
            }
            Settings.overlayPos = UI.MousePositionOnUIInverted - dragOffset;
        }

        private static void DoContents(Rect rect, List<Ticket> items, float textWidth)
        {
            Widgets.DrawBoxSolid(rect, Background);

            // Header: label, then collapse and hide buttons on the right.
            Rect header = new Rect(rect.x + Pad, rect.y, rect.width - Pad * 2f, HeaderHeight);
            Rect hideRect = new Rect(header.xMax - IconSize, header.y + 3f, IconSize, IconSize);
            Rect collapseRect = new Rect(hideRect.x - IconSize - 6f, header.y + 3f, IconSize, IconSize);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HeaderText;
            // Resolve first: appending raw color tags to a TaggedString escapes them.
            string label = "RM_Overlay_Header".Translate().Resolve();
            Widgets.Label(header, label + "  " + items.Count.ToString().Colorize(Widgets.InactiveColor));
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            if (Widgets.ButtonImage(collapseRect, Settings.overlayCollapsed ? TexButton.Reveal : TexButton.Collapse, new Color(1f, 1f, 1f, 0.6f), Color.white))
            {
                Settings.overlayCollapsed = !Settings.overlayCollapsed;
                RimMinderMod.SaveSettings();
            }
            TooltipHandler.TipRegion(collapseRect, Settings.overlayCollapsed ? "RM_Overlay_Expand".Translate() : "RM_Overlay_Collapse".Translate());
            if (Widgets.ButtonImage(hideRect, TexButton.CloseXSmall, new Color(1f, 1f, 1f, 0.6f), Color.white))
            {
                Settings.showOverlay = false;
                RimMinderMod.SaveSettings();
            }
            TooltipHandler.TipRegion(hideRect, "RM_Overlay_Hide".Translate());
            GUI.color = Color.white;

            Rect dragArea = new Rect(rect.x, rect.y, collapseRect.x - rect.x - 4f, HeaderHeight);
            TooltipHandler.TipRegion(dragArea, Settings.overlayLocked ? "RM_Overlay_HeaderTipLocked".Translate() : "RM_Overlay_HeaderTip".Translate());
            HandleHeaderInput(dragArea);

            if (!Settings.overlayCollapsed)
            {
                float y = rect.y + HeaderHeight + 2f;
                foreach (Ticket t in items)
                {
                    float h = RowHeight(t, textWidth);
                    Rect row = new Rect(rect.x + Pad, y, rect.width - Pad * 2f, h);
                    DrawRow(row, t, textWidth);
                    y += h + RowGap;
                }
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private static void HandleHeaderInput(Rect dragArea)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || !Mouse.IsOver(dragArea))
                return;
            if (e.button == 0 && Settings.overlayLocked)
            {
                RimMinderDefOf.ToggleBoard();
                e.Use();
            }
            else if (e.button == 0)
            {
                dragging = true;
                pressStart = UI.MousePositionOnUIInverted;
                dragOffset = UI.MousePositionOnUIInverted - Settings.overlayPos;
                if (Settings.overlayPos.x < 0f)
                {
                    // First drag from the default spot: start from where it's drawn now.
                    Settings.overlayPos = UI.MousePositionOnUIInverted - e.mousePosition;
                    dragOffset = e.mousePosition;
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(Settings.overlayLocked ? "RM_Overlay_Unlock".Translate() : "RM_Overlay_Lock".Translate(), () =>
                    {
                        Settings.overlayLocked = !Settings.overlayLocked;
                        RimMinderMod.SaveSettings();
                    }),
                    new FloatMenuOption("RM_Overlay_Hide".Translate(), () =>
                    {
                        Settings.showOverlay = false;
                        RimMinderMod.SaveSettings();
                    })
                };
                Find.WindowStack.Add(new FloatMenu(options));
                e.Use();
            }
        }

        private static void DrawRow(Rect row, Ticket t, float textWidth)
        {
            Widgets.DrawBoxSolid(new Rect(row.x, row.y + 1f, Stripe, row.height - 2f), t.Color);
            Widgets.DrawHighlightIfMouseover(row);

            Text.Font = GameFont.Tiny;
            float titleHeight = Text.CalcHeight(t.title, textWidth);
            Rect textRect = new Rect(row.x + Stripe + 5f, row.y, textWidth, row.height);
            GUI.color = Color.white;
            Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, titleHeight), t.title);
            string meta = t.MetaLabel(out Color metaColor);
            GUI.color = metaColor;
            Widgets.Label(new Rect(textRect.x, textRect.y + titleHeight - 2f, textRect.width, MetaHeight), meta);
            GUI.color = Color.white;

            if (!t.notes.NullOrEmpty())
                TooltipHandler.TipRegion(row, t.notes);
            if (Widgets.ButtonInvisible(row))
                Find.MainTabsRoot.SetCurrentTab(RimMinderDefOf.RimMinder_Plans);
        }
    }
}
