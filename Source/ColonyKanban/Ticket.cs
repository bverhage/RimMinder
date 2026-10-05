using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyKanban
{
    public enum TicketStatus
    {
        Plan,
        Doing,
        Done
    }

    public class Ticket : IExposable
    {
        public int id;
        public string title = "";
        public string notes = "";
        public TicketStatus status = TicketStatus.Plan;
        public int colorIndex;
        public int createdTick;
        public int doneTick = -1;
        public int deadlineTick = -1;
        public bool overdueNotified;

        // Muted colors that sit well next to vanilla UI.
        public static readonly Color[] Palette =
        {
            new Color(0.55f, 0.55f, 0.55f), // grey
            new Color(0.31f, 0.56f, 0.82f), // blue
            new Color(0.85f, 0.69f, 0.23f), // yellow
            new Color(0.42f, 0.66f, 0.31f), // green
            new Color(0.75f, 0.31f, 0.30f), // red
            new Color(0.60f, 0.43f, 0.75f), // purple
        };

        public static readonly Color WarnColor = new Color(0.9f, 0.75f, 0.3f);
        public static readonly Color OverdueColor = new Color(0.9f, 0.42f, 0.35f);

        public Color Color => Palette[Mathf.Clamp(colorIndex, 0, Palette.Length - 1)];

        public bool HasDeadline => deadlineTick >= 0;

        public bool IsOverdue => HasDeadline && status != TicketStatus.Done && Now > deadlineTick;

        public float AgeDays => (Now - createdTick) / (float)GenDate.TicksPerDay;

        public float DaysLeft => (deadlineTick - Now) / (float)GenDate.TicksPerDay;

        private static int Now => Find.TickManager.TicksGame;

        public static string FormatDays(float days)
        {
            return Mathf.Abs(days) < 10f ? days.ToString("0.0") : days.ToString("0");
        }

        /// <summary>The short grey line under a card's title, with the color it should be drawn in.</summary>
        public string MetaLabel(out Color color)
        {
            color = Widgets.InactiveColor;
            if (status == TicketStatus.Done)
            {
                int end = doneTick >= 0 ? doneTick : Now;
                return "CK_Meta_Took".Translate(FormatDays((end - createdTick) / (float)GenDate.TicksPerDay));
            }
            string age = "CK_Meta_Age".Translate(FormatDays(AgeDays));
            if (!HasDeadline)
                return age;
            float left = DaysLeft;
            if (left < 0f)
            {
                color = OverdueColor;
                return age + " · " + "CK_Meta_Overdue".Translate(FormatDays(-left));
            }
            if (left < 1f)
                color = WarnColor;
            return age + " · " + "CK_Meta_DueIn".Translate(FormatDays(left));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref title, "title", "");
            Scribe_Values.Look(ref notes, "notes", "");
            Scribe_Values.Look(ref status, "status", TicketStatus.Plan);
            Scribe_Values.Look(ref colorIndex, "colorIndex");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref doneTick, "doneTick", -1);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref overdueNotified, "overdueNotified");
        }
    }
}
