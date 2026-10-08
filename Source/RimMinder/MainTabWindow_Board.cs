using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimMinder
{
    public class MainTabWindow_Board : MainTabWindow
    {
        private const float HeaderHeight = 28f;
        private const float ColumnHeaderHeight = 24f;
        private const float ColumnGap = 6f;
        private const float CardGap = 4f;
        private const float CardPad = 4f;
        private const float StripeWidth = 3f;
        private const float MetaHeight = 16f;
        private const float DragThreshold = 6f;
        private const float BarHeight = 4f;
        private const float GripSize = 16f;

        public static readonly Vector2 DefaultSize = new Vector2(560f, 320f);
        private static readonly Vector2 MinSize = new Vector2(460f, 240f);

        private static readonly Color CardBg = new Color(0.12f, 0.135f, 0.15f);
        private static readonly Color CardBorder = new Color(0.25f, 0.27f, 0.29f);
        private static readonly Color DropLine = new Color(0.85f, 0.85f, 0.85f, 0.8f);

        private static readonly TicketStatus[] Columns = { TicketStatus.Plan, TicketStatus.Doing, TicketStatus.Done };

        private readonly Vector2[] scroll = new Vector2[3];

        // Layout of the last drawn frame, in window coordinates, used for hit-testing and drops.
        private readonly Rect[] columnRects = new Rect[3];
        private readonly List<(Ticket ticket, Rect rect, int column)> cardRects = new List<(Ticket, Rect, int)>();

        private Ticket pressed;
        private Vector2 pressStart;
        private Vector2 grabOffset;
        private bool dragging;

        private bool resizing;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;

        public override Vector2 RequestedTabSize => ClampSize(RimMinderMod.Settings.boardSize);

        private static Vector2 ClampSize(Vector2 size)
        {
            // Leave room for the bottom bar and a little space at the top.
            return new Vector2(
                Mathf.Clamp(size.x, MinSize.x, UI.screenWidth),
                Mathf.Clamp(size.y, MinSize.y, UI.screenHeight - 35f - 40f));
        }

        public MainTabWindow_Board()
        {
            forcePause = false;
        }

        public static string ColumnLabel(TicketStatus status) => ("RM_Status_" + status).Translate();

        public override void PostClose()
        {
            base.PostClose();
            ResetDrag();
            if (resizing)
            {
                resizing = false;
                RimMinderMod.SaveSettings();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            BoardComponent board = BoardComponent.Get();
            if (board == null)
                return;

            DoHeader(new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight), board);

            Rect body = new Rect(inRect.x, inRect.y + HeaderHeight + 4f, inRect.width, inRect.height - HeaderHeight - 4f);
            float colWidth = (body.width - ColumnGap * (Columns.Length - 1)) / Columns.Length;
            cardRects.Clear();
            for (int i = 0; i < Columns.Length; i++)
            {
                Rect col = new Rect(body.x + i * (colWidth + ColumnGap), body.y, colWidth, body.height);
                columnRects[i] = col;
                DoColumn(col, i, board);
            }

            HandleInput(board);
            DrawDragFeedback(board);
            DoResizeGrip(inRect);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DoHeader(Rect rect, BoardComponent board)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, "RM_BoardTitle".Translate());
            Text.Font = GameFont.Small;

            Rect newButton = new Rect(rect.xMax - GripSize - 8f - 120f, rect.y + 2f, 120f, 24f);
            if (Widgets.ButtonText(newButton, "RM_NewTicket".Translate()))
                Find.WindowStack.Add(new Dialog_EditTicket(board.Create(), isNew: true));

            string reminder = "RM_ShowReminder".Translate();
            float reminderWidth = Text.CalcSize(reminder).x + 34f;
            Rect reminderRect = new Rect(newButton.x - reminderWidth - 12f, rect.y + 2f, reminderWidth, 24f);
            bool show = RimMinderMod.Settings.showOverlay;
            Widgets.CheckboxLabeled(reminderRect, reminder, ref show);
            if (show != RimMinderMod.Settings.showOverlay)
            {
                RimMinderMod.Settings.showOverlay = show;
                RimMinderMod.SaveSettings();
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DoColumn(Rect rect, int index, BoardComponent board)
        {
            TicketStatus status = Columns[index];
            List<Ticket> tickets = board.InColumn(status).ToList();

            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(4f);

            Rect header = new Rect(inner.x, inner.y, inner.width, ColumnHeaderHeight - 4f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(header, ColumnLabel(status) + " <color=#888888>(" + tickets.Count + ")</color>");
            GUI.color = Color.white;

            if (status == TicketStatus.Done && tickets.Count > 0)
            {
                Text.Font = GameFont.Tiny;
                string clear = "RM_ClearDone".Translate();
                float w = Text.CalcSize(clear).x + 10f;
                Rect clearRect = new Rect(header.xMax - w, header.y, w, header.height);
                if (Widgets.ButtonText(clearRect, clear, drawBackground: false))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RM_ClearDoneConfirm".Translate(tickets.Count),
                        () => board.tickets.RemoveAll(t => t.status == TicketStatus.Done),
                        destructive: true));
                }
                Text.Font = GameFont.Small;
            }
            Text.Anchor = TextAnchor.UpperLeft;

            Rect outRect = new Rect(inner.x, inner.y + ColumnHeaderHeight, inner.width, inner.height - ColumnHeaderHeight);
            float cardWidth = outRect.width - 16f;
            float viewHeight = 0f;
            foreach (Ticket t in tickets)
                viewHeight += CardHeight(t, cardWidth) + CardGap;
            Rect viewRect = new Rect(0f, 0f, cardWidth, Mathf.Max(viewHeight, outRect.height));

            if (tickets.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Widgets.InactiveColor;
                string empty = board.tickets.Count == 0 && status == TicketStatus.Plan ? "RM_EmptyBoard".Translate() : "RM_EmptyColumn".Translate();
                Widgets.Label(new Rect(outRect.x + 6f, outRect.y + 10f, outRect.width - 12f, 60f), empty);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            Widgets.BeginScrollView(outRect, ref scroll[index], viewRect);
            float y = 0f;
            foreach (Ticket t in tickets)
            {
                float h = CardHeight(t, cardWidth);
                Rect card = new Rect(0f, y, cardWidth, h);
                bool isDragged = dragging && t == pressed;
                DrawCard(card, t, isDragged ? 0.3f : 1f, highlight: !dragging);
                if (status == TicketStatus.Done && !dragging && Mouse.IsOver(card))
                {
                    Rect deleteRect = new Rect(card.xMax - 16f, card.y + 4f, 12f, 12f);
                    TooltipHandler.TipRegion(deleteRect, "RM_Delete".Translate());
                    if (Widgets.ButtonImage(deleteRect, TexButton.CloseXSmall, new Color(1f, 1f, 1f, 0.6f), Color.white))
                        board.Remove(t);
                }
                if (!t.notes.NullOrEmpty() && !dragging)
                    TooltipHandler.TipRegion(card, t.notes);
                cardRects.Add((t, new Rect(outRect.x + card.x, outRect.y + card.y - scroll[index].y, card.width, card.height), index));
                y += h + CardGap;
            }
            Widgets.EndScrollView();
        }

        public static float CardHeight(Ticket t, float width)
        {
            float textWidth = width - StripeWidth - CardPad * 3f;
            Text.Font = GameFont.Small;
            float titleHeight = Text.CalcHeight(t.title, textWidth);
            return CardPad + titleHeight + MetaLineHeight(t, textWidth) + (t.IsCountGoal ? BarHeight + 2f : 0f) + CardPad;
        }

        /// <summary>The meta line wraps on narrow cards ("last raid 2.1 d ago · repeats"), so measure it.</summary>
        private static float MetaLineHeight(Ticket t, float textWidth)
        {
            Text.Font = GameFont.Tiny;
            float height = Mathf.Max(MetaHeight, Text.CalcHeight(t.MetaLabel(out _), textWidth));
            Text.Font = GameFont.Small;
            return height;
        }

        public static void DrawCard(Rect rect, Ticket t, float alpha = 1f, bool highlight = true)
        {
            Color old = GUI.color;
            Widgets.DrawBoxSolid(rect, CardBg.WithAlpha(alpha));
            GUI.color = CardBorder.WithAlpha(alpha);
            Widgets.DrawBox(rect);
            Color stripe = t.status == TicketStatus.Done ? Color.Lerp(t.Color, Color.gray, 0.6f) : t.Color;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, StripeWidth, rect.height), stripe.WithAlpha(alpha));
            if (highlight)
                Widgets.DrawHighlightIfMouseover(rect);

            Rect textRect = new Rect(rect.x + StripeWidth + CardPad * 2f, rect.y + CardPad, rect.width - StripeWidth - CardPad * 3f, rect.height - CardPad * 2f);
            Text.Font = GameFont.Small;
            GUI.color = (t.status == TicketStatus.Done ? Widgets.InactiveColor : Color.white).WithAlpha(alpha);
            float titleHeight = Text.CalcHeight(t.title, textRect.width);
            Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, titleHeight), t.title);

            float metaHeight = MetaLineHeight(t, textRect.width);
            Text.Font = GameFont.Tiny;
            string meta = t.MetaLabel(out Color metaColor);
            GUI.color = metaColor.WithAlpha(alpha);
            Widgets.Label(new Rect(textRect.x, textRect.y + titleHeight, textRect.width, metaHeight), meta);

            if (t.IsCountGoal)
            {
                Rect bar = new Rect(textRect.x, textRect.y + titleHeight + metaHeight + 1f, textRect.width, BarHeight);
                Widgets.DrawBoxSolid(bar, new Color(0.18f, 0.2f, 0.22f, alpha));
                Color fill = t.GoalMet ? Ticket.MetColor : t.Color;
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * t.Progress, bar.height), fill.WithAlpha(alpha));
            }

            Text.Font = GameFont.Small;
            GUI.color = old;
        }

        private Ticket CardAt(Vector2 pos)
        {
            for (int i = 0; i < cardRects.Count; i++)
            {
                var c = cardRects[i];
                if (c.rect.Contains(pos) && columnRects[c.column].Contains(pos))
                    return c.ticket;
            }
            return null;
        }

        private int ColumnAt(Vector2 pos)
        {
            for (int i = 0; i < columnRects.Length; i++)
                if (columnRects[i].Contains(pos))
                    return i;
            return -1;
        }

        /// <summary>The ticket the dragged one would be inserted before, and the y of the drop line.</summary>
        private Ticket DropBefore(int column, Vector2 pos, out float lineY)
        {
            lineY = columnRects[column].y + ColumnHeaderHeight + 2f;
            foreach (var c in cardRects)
            {
                if (c.column != column || c.ticket == pressed)
                    continue;
                if (pos.y < c.rect.center.y)
                {
                    lineY = c.rect.y - CardGap / 2f;
                    return c.ticket;
                }
                lineY = c.rect.yMax + CardGap / 2f;
            }
            return null;
        }

        private void HandleInput(BoardComponent board)
        {
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            if (pressed != null && !Input.GetMouseButton(0) && e.type == EventType.Repaint)
            {
                // Released outside the window.
                ResetDrag();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ticket hit = CardAt(mouse);
                if (hit != null)
                {
                    pressed = hit;
                    pressStart = mouse;
                    grabOffset = mouse - cardRects.First(c => c.ticket == hit).rect.position;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                Ticket hit = CardAt(mouse);
                if (hit != null)
                {
                    OpenContextMenu(hit, board);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && pressed != null)
            {
                if (!dragging && (mouse - pressStart).magnitude > DragThreshold)
                    dragging = true;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && pressed != null)
            {
                if (dragging)
                {
                    int col = ColumnAt(mouse);
                    if (col >= 0)
                    {
                        board.Move(pressed, Columns[col], DropBefore(col, mouse, out _));
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_EditTicket(pressed, isNew: false));
                }
                ResetDrag();
                e.Use();
            }
        }

        private void DrawDragFeedback(BoardComponent board)
        {
            if (!dragging || pressed == null)
                return;
            Vector2 mouse = Event.current.mousePosition;
            int col = ColumnAt(mouse);
            if (col >= 0)
            {
                Rect colRect = columnRects[col];
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.DrawBox(colRect.ContractedBy(1f));
                DropBefore(col, mouse, out float lineY);
                Widgets.DrawLineHorizontal(colRect.x + 6f, lineY, colRect.width - 12f, DropLine);
                GUI.color = Color.white;
            }

            var source = cardRects.FirstOrDefault(c => c.ticket == pressed);
            float width = source.ticket != null ? source.rect.width : columnRects[0].width - 24f;
            Rect ghost = new Rect(mouse - grabOffset, new Vector2(width, CardHeight(pressed, width)));
            DrawCard(ghost, pressed, 0.75f, highlight: false);
        }

        private void OpenContextMenu(Ticket ticket, BoardComponent board)
        {
            var options = new List<FloatMenuOption>();
            foreach (TicketStatus status in Columns)
            {
                if (status == ticket.status)
                    continue;
                TicketStatus target = status;
                options.Add(new FloatMenuOption("RM_MoveTo".Translate(ColumnLabel(target)), () => board.Move(ticket, target, null)));
            }
            options.Add(new FloatMenuOption("RM_Edit".Translate(), () => Find.WindowStack.Add(new Dialog_EditTicket(ticket, isNew: false))));
            options.Add(new FloatMenuOption("RM_Delete".Translate(), () => ConfirmDelete(ticket, board)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static void ConfirmDelete(Ticket ticket, BoardComponent board)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RM_DeleteConfirm".Translate(ticket.title),
                () => board.Remove(ticket),
                destructive: true));
        }

        /// <summary>
        /// The window is anchored bottom-left, so the grip sits top-right:
        /// dragging it grows the width to the right and the height upwards.
        /// </summary>
        private void DoResizeGrip(Rect inRect)
        {
            // Top-right corner of the content area; the header leaves room for it.
            Rect grip = new Rect(inRect.xMax - GripSize, inRect.y, GripSize, GripSize);

            GUI.color = Mouse.IsOver(grip) || resizing ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTextureWithTexCoords(grip, TexUI.WinExpandWidget, new Rect(0f, 1f, 1f, -1f));
            GUI.color = Color.white;
            TooltipHandler.TipRegion(grip, "RM_ResizeTip".Translate());

            Event e = Event.current;
            if (e.type == EventType.MouseDown && Mouse.IsOver(grip))
            {
                if (e.button == 0)
                {
                    resizing = true;
                    resizeStartMouse = UI.MousePositionOnUIInverted;
                    resizeStartSize = windowRect.size;
                }
                else if (e.button == 1)
                {
                    ApplySize(DefaultSize);
                    RimMinderMod.SaveSettings();
                }
                e.Use();
            }

            if (!resizing)
                return;
            if (!Input.GetMouseButton(0))
            {
                resizing = false;
                RimMinderMod.SaveSettings();
                return;
            }
            Vector2 delta = UI.MousePositionOnUIInverted - resizeStartMouse;
            ApplySize(new Vector2(resizeStartSize.x + delta.x, resizeStartSize.y - delta.y));
        }

        private void ApplySize(Vector2 size)
        {
            size = ClampSize(size);
            float bottom = windowRect.yMax;
            windowRect = new Rect(windowRect.x, bottom - size.y, size.x, size.y);
            RimMinderMod.Settings.boardSize = size;
        }

        private void ResetDrag()
        {
            pressed = null;
            dragging = false;
        }
    }
}
