using RimWorld;
using UnityEngine;
using Verse;

namespace RimMinder
{
    public class Dialog_EditTicket : Window
    {
        private const string TitleControl = "RM_TicketTitle";
        private const float RowHeight = 28f;
        private const float LabelWidth = 120f;
        private const float BaseHeight = 370f;
        private const float LinkSectionHeight = 96f;

        private readonly Ticket ticket;
        private readonly bool isNew;

        private string title;
        private string notes;
        private int colorIndex;
        private bool hasDeadline;
        private float deadlineDays;
        private string deadlineBuffer;
        private bool focused;
        private bool showError;

        // Link to game, kept behind a collapsed section so plain tickets stay simple.
        private bool linkOpen;
        private LinkType link;
        private ThingDef countThing;
        private ThingCategoryDef countCategory;
        private int countTarget;
        private string countBuffer;
        private bool doneWhenReached;
        private EventKind eventKind;
        private bool reopenOnEvent;

        public override Vector2 InitialSize => new Vector2(480f, BaseHeight + (linkOpen ? LinkSectionHeight : 0f));

        public Dialog_EditTicket(Ticket ticket, bool isNew)
        {
            this.ticket = ticket;
            this.isNew = isNew;
            title = ticket.title;
            notes = ticket.notes;
            colorIndex = ticket.colorIndex;
            hasDeadline = ticket.HasDeadline;
            deadlineDays = hasDeadline ? Mathf.Max(0f, (float)System.Math.Round(ticket.DaysLeft, 1)) : 3f;
            deadlineBuffer = deadlineDays.ToString();

            link = ticket.link;
            countThing = ticket.countThing;
            countCategory = ticket.countCategory;
            countTarget = ticket.countTarget;
            countBuffer = countTarget.ToString();
            doneWhenReached = ticket.doneWhenReached;
            eventKind = ticket.eventKind;
            reopenOnEvent = ticket.reopenOnEvent;
            // Plain tickets keep the link section out of the way; linked ones show what they're tracking.
            linkOpen = link != LinkType.None;

            forcePause = false;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            optionalTitle = isNew ? "RM_NewTitle".Translate() : "RM_EditTitle".Translate();
        }

        public override void OnAcceptKeyPressed()
        {
            // Enter saves, except inside the multi-line notes field.
            if (GUI.GetNameOfFocusedControl() == TitleControl || GUI.GetNameOfFocusedControl().NullOrEmpty())
            {
                Save();
                Event.current.Use();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            FitHeight();
            Text.Font = GameFont.Small;
            float y = inRect.y;

            // Title
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, LabelWidth, RowHeight), "RM_Field_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Rect titleRect = new Rect(inRect.x + LabelWidth, y, inRect.width - LabelWidth, RowHeight);
            GUI.SetNextControlName(TitleControl);
            string newTitle = Widgets.TextField(titleRect, title);
            if (newTitle != title)
            {
                title = newTitle;
                showError = false;
            }
            if (title.NullOrEmpty() && GUI.GetNameOfFocusedControl() != TitleControl)
            {
                GUI.color = Widgets.InactiveColor;
                Widgets.Label(titleRect.ContractedBy(4f, 3f), "RM_TitlePlaceholder".Translate());
                GUI.color = Color.white;
            }
            if (!focused)
            {
                UI.FocusControl(TitleControl, this);
                focused = true;
            }
            y += RowHeight + 6f;

            // Notes
            Widgets.Label(new Rect(inRect.x, y + 4f, LabelWidth, RowHeight), "RM_Field_Notes".Translate());
            notes = Widgets.TextArea(new Rect(inRect.x + LabelWidth, y, inRect.width - LabelWidth, 80f), notes);
            y += 80f + 8f;

            // Color
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, LabelWidth, RowHeight), "RM_Field_Color".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            for (int i = 0; i < Ticket.Palette.Length; i++)
            {
                Rect swatch = new Rect(inRect.x + LabelWidth + i * 30f, y + 3f, 22f, 22f);
                Widgets.DrawBoxSolid(swatch, Ticket.Palette[i]);
                if (i == colorIndex)
                    Widgets.DrawBox(swatch.ExpandedBy(2f), 2);
                else
                    Widgets.DrawHighlightIfMouseover(swatch);
                if (Widgets.ButtonInvisible(swatch))
                    colorIndex = i;
            }
            y += RowHeight + 6f;

            // Deadline
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, LabelWidth + 24f, RowHeight), "RM_Field_Deadline".Translate(), ref hasDeadline, placeCheckboxNearText: true);
            if (hasDeadline)
            {
                Rect field = new Rect(inRect.x + LabelWidth + 34f, y + 2f, 60f, RowHeight - 4f);
                Widgets.TextFieldNumeric(field, ref deadlineDays, ref deadlineBuffer, 0f, 999f);
            }
            y += RowHeight + 6f;

            y = DoLinkSection(inRect, y);

            if (showError)
            {
                GUI.color = Ticket.OverdueColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(inRect.x + LabelWidth, y, inRect.width - LabelWidth, 20f), "RM_NeedTitle".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Buttons
            Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
            if (!isNew)
            {
                if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 90f, 32f), "RM_Delete".Translate()))
                {
                    BoardComponent board = BoardComponent.Get();
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RM_DeleteConfirm".Translate(ticket.title),
                        () =>
                        {
                            board?.Remove(ticket);
                            Close();
                        },
                        destructive: true));
                }
            }
            if (Widgets.ButtonText(new Rect(buttons.xMax - 200f, buttons.y, 95f, 32f), "RM_Cancel".Translate()))
                Close();
            if (Widgets.ButtonText(new Rect(buttons.xMax - 95f, buttons.y, 95f, 32f), "RM_Save".Translate()))
                Save();
        }

        /// <summary>Grows or shrinks the window when the link section opens or closes, keeping it centered.</summary>
        private void FitHeight()
        {
            float wanted = BaseHeight + (linkOpen ? LinkSectionHeight : 0f);
            if (Mathf.Approximately(windowRect.height, wanted))
                return;
            float delta = wanted - windowRect.height;
            windowRect = new Rect(windowRect.x, windowRect.y - delta / 2f, windowRect.width, wanted);
        }

        private string LinkSummary()
        {
            switch (link)
            {
                case LinkType.Count:
                    return "RM_LinkSummary_Count".Translate(countTarget, GameLinks.CountLabel(countThing, countCategory));
                case LinkType.Event:
                    return "RM_LinkSummary_Event".Translate(GameLinks.Label(eventKind));
                default:
                    return "RM_Link_None".Translate();
            }
        }

        private float DoLinkSection(Rect inRect, float y)
        {
            // Collapsed: one quiet row with a summary. Click to open.
            Rect header = new Rect(inRect.x, y, inRect.width, RowHeight);
            Widgets.DrawHighlightIfMouseover(header);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(header.x, header.y, LabelWidth, header.height), (linkOpen ? "▾ " : "▸ ") + "RM_Field_Link".Translate());
            GUI.color = link == LinkType.None ? Widgets.InactiveColor : Color.white;
            Widgets.Label(new Rect(header.x + LabelWidth, header.y, header.width - LabelWidth, header.height), LinkSummary());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(header))
                linkOpen = !linkOpen;
            y += RowHeight + 2f;
            if (!linkOpen)
                return y;

            float x = inRect.x + LabelWidth;
            float w = inRect.width - LabelWidth;

            // Type: three radio buttons in a row, circle first and each sized to its label
            // (vanilla's RadioButtonLabeled puts the circle on the right and squeezes the text).
            float cx = x;
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (LinkType type in new[] { LinkType.None, LinkType.Count, LinkType.Event })
            {
                string label = GameLinks.Label(type);
                float labelWidth = Text.CalcSize(label).x;
                Rect option = new Rect(cx, y, Widgets.RadioButtonSize + 4f + labelWidth, RowHeight);
                Widgets.DrawHighlightIfMouseover(option);
                Widgets.RadioButton(option.x, option.y + (RowHeight - Widgets.RadioButtonSize) / 2f, link == type);
                Widgets.Label(new Rect(option.x + Widgets.RadioButtonSize + 4f, option.y, labelWidth, RowHeight), label);
                if (Widgets.ButtonInvisible(option) && link != type)
                {
                    link = type;
                    if (link == LinkType.Count && countThing == null && countCategory == null)
                        countCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
                }
                cx = option.xMax + 14f;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            y += RowHeight + 4f;

            Text.Anchor = TextAnchor.MiddleLeft;
            if (link == LinkType.Count)
            {
                Widgets.Label(new Rect(x, y, 40f, RowHeight), "RM_Have".Translate());
                Widgets.TextFieldNumeric(new Rect(x + 40f, y + 2f, 60f, RowHeight - 4f), ref countTarget, ref countBuffer, 1, 999999);
                if (Widgets.ButtonText(new Rect(x + 108f, y, w - 108f, RowHeight), GameLinks.CountLabel(countThing, countCategory)))
                {
                    GameLinks.OpenCountPicker((thing, category) =>
                    {
                        countThing = thing;
                        countCategory = category;
                    });
                }
                y += RowHeight + 4f;
                Widgets.CheckboxLabeled(new Rect(x, y, w, RowHeight), "RM_DoneWhenReached".Translate(), ref doneWhenReached, placeCheckboxNearText: true);
            }
            else if (link == LinkType.Event)
            {
                Widgets.Label(new Rect(x, y, 70f, RowHeight), "RM_SinceLast".Translate());
                if (Widgets.ButtonText(new Rect(x + 70f, y, w - 70f, RowHeight), GameLinks.Label(eventKind)))
                {
                    var options = new System.Collections.Generic.List<FloatMenuOption>();
                    foreach (EventKind kind in GameLinks.AllEventKinds)
                    {
                        EventKind k = kind;
                        options.Add(new FloatMenuOption(GameLinks.Label(k), () => eventKind = k));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += RowHeight + 4f;
                Widgets.CheckboxLabeled(new Rect(x, y, w, RowHeight), "RM_ReopenOnEvent".Translate(), ref reopenOnEvent, placeCheckboxNearText: true);
            }
            else
            {
                GUI.color = Widgets.InactiveColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, y, w, RowHeight * 2f), "RM_LinkHelp".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            return y + RowHeight + 6f;
        }

        private void Save()
        {
            if (title.NullOrEmpty() || title.Trim().Length == 0)
            {
                showError = true;
                return;
            }
            BoardComponent board = BoardComponent.Get();
            if (board == null)
            {
                Close();
                return;
            }

            int oldDeadline = ticket.deadlineTick;
            ticket.title = title.Trim();
            ticket.notes = notes ?? "";
            ticket.colorIndex = colorIndex;
            if (hasDeadline)
            {
                bool unchanged = ticket.HasDeadline && Mathf.Approximately(deadlineDays, Mathf.Max(0f, (float)System.Math.Round(ticket.DaysLeft, 1)));
                if (!unchanged)
                    ticket.deadlineTick = Find.TickManager.TicksGame + Mathf.RoundToInt(deadlineDays * GenDate.TicksPerDay);
            }
            else
            {
                ticket.deadlineTick = -1;
            }
            if (ticket.deadlineTick != oldDeadline)
                ticket.overdueNotified = false;

            ticket.link = link;
            ticket.countThing = countThing;
            ticket.countCategory = countCategory;
            ticket.countTarget = countTarget;
            ticket.doneWhenReached = doneWhenReached;
            ticket.eventKind = eventKind;
            ticket.reopenOnEvent = reopenOnEvent;

            if (isNew)
                board.Add(ticket);
            Close();
        }
    }
}
