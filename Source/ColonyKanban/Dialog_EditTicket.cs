using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyKanban
{
    public class Dialog_EditTicket : Window
    {
        private const string TitleControl = "CK_TicketTitle";
        private const float RowHeight = 28f;
        private const float LabelWidth = 120f;

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

        public override Vector2 InitialSize => new Vector2(420f, 340f);

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

            forcePause = false;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            optionalTitle = isNew ? "CK_NewTitle".Translate() : "CK_EditTitle".Translate();
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
            Text.Font = GameFont.Small;
            float y = inRect.y;

            // Title
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, LabelWidth, RowHeight), "CK_Field_Title".Translate());
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
                Widgets.Label(titleRect.ContractedBy(4f, 3f), "CK_TitlePlaceholder".Translate());
                GUI.color = Color.white;
            }
            if (!focused)
            {
                UI.FocusControl(TitleControl, this);
                focused = true;
            }
            y += RowHeight + 6f;

            // Notes
            Widgets.Label(new Rect(inRect.x, y + 4f, LabelWidth, RowHeight), "CK_Field_Notes".Translate());
            notes = Widgets.TextArea(new Rect(inRect.x + LabelWidth, y, inRect.width - LabelWidth, 80f), notes);
            y += 80f + 8f;

            // Color
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, y, LabelWidth, RowHeight), "CK_Field_Color".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            for (int i = 0; i < Ticket.Palette.Length; i++)
            {
                Rect swatch = new Rect(inRect.x + LabelWidth + i * 30f, y + 3f, 22f, 22f);
                Widgets.DrawBoxSolid(swatch, Ticket.Palette[i]);
                if (i == colorIndex)
                {
                    Widgets.DrawBox(swatch.ExpandedBy(2f), 2);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(swatch);
                }
                if (Widgets.ButtonInvisible(swatch))
                    colorIndex = i;
            }
            y += RowHeight + 6f;

            // Deadline
            Rect deadlineRow = new Rect(inRect.x, y, inRect.width, RowHeight);
            Widgets.CheckboxLabeled(new Rect(deadlineRow.x, deadlineRow.y, LabelWidth + 24f, RowHeight), "CK_Field_Deadline".Translate(), ref hasDeadline, placeCheckboxNearText: true);
            if (hasDeadline)
            {
                Rect field = new Rect(inRect.x + LabelWidth + 34f, y + 2f, 60f, RowHeight - 4f);
                Widgets.TextFieldNumeric(field, ref deadlineDays, ref deadlineBuffer, 0f, 999f);
            }
            y += RowHeight + 6f;

            if (showError)
            {
                GUI.color = Ticket.OverdueColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(inRect.x + LabelWidth, y, inRect.width - LabelWidth, 20f), "CK_NeedTitle".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Buttons
            Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
            if (!isNew)
            {
                if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 90f, 32f), "CK_Delete".Translate()))
                {
                    KanbanGameComponent board = KanbanGameComponent.Get();
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "CK_DeleteConfirm".Translate(ticket.title),
                        () =>
                        {
                            board?.Remove(ticket);
                            Close();
                        },
                        destructive: true));
                }
            }
            if (Widgets.ButtonText(new Rect(buttons.xMax - 200f, buttons.y, 95f, 32f), "CK_Cancel".Translate()))
                Close();
            if (Widgets.ButtonText(new Rect(buttons.xMax - 95f, buttons.y, 95f, 32f), "CK_Save".Translate()))
                Save();
        }

        private void Save()
        {
            if (title.NullOrEmpty() || title.Trim().Length == 0)
            {
                showError = true;
                return;
            }
            KanbanGameComponent board = KanbanGameComponent.Get();
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

            if (isNew)
                board.Add(ticket);
            Close();
        }
    }
}
