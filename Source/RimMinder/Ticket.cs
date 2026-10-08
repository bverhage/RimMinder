using RimWorld;
using UnityEngine;
using Verse;

namespace RimMinder
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

        // Optional link to game state.
        public LinkType link = LinkType.None;
        public ThingDef countThing;
        public ThingCategoryDef countCategory;
        public int countTarget = 30;
        public bool doneWhenReached;
        public EventKind eventKind = EventKind.Raid;
        public bool reopenOnEvent;

        private int cachedCount;
        private float cachedCountTime = -999f;

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
        public static readonly Color MetColor = new Color(0.5f, 0.75f, 0.37f);

        public Color Color => Palette[Mathf.Clamp(colorIndex, 0, Palette.Length - 1)];

        public bool HasDeadline => deadlineTick >= 0;

        public bool IsOverdue => HasDeadline && status != TicketStatus.Done && Now > deadlineTick;

        public float AgeDays => (Now - createdTick) / (float)GenDate.TicksPerDay;

        public float DaysLeft => (deadlineTick - Now) / (float)GenDate.TicksPerDay;

        public bool IsCountGoal => link == LinkType.Count && (countThing != null || countCategory != null);

        /// <summary>Stockpile count for a count goal, refreshed at most once a second (also while paused).</summary>
        public int CurrentCount
        {
            get
            {
                if (Time.realtimeSinceStartup - cachedCountTime > 1f)
                {
                    cachedCount = GameLinks.Count(countThing, countCategory);
                    cachedCountTime = Time.realtimeSinceStartup;
                }
                return cachedCount;
            }
        }

        public bool GoalMet => IsCountGoal && CurrentCount >= countTarget;

        /// <summary>0..1 progress for the card's bar.</summary>
        public float Progress => countTarget <= 0 ? 1f : Mathf.Clamp01(CurrentCount / (float)countTarget);

        private static int Now => Find.TickManager.TicksGame;

        public static string FormatDays(float days)
        {
            return Mathf.Abs(days) < 10f ? days.ToString("0.0") : days.ToString("0");
        }

        /// <summary>The short line under a card's title, with the color it should be drawn in.</summary>
        public string MetaLabel(out Color color)
        {
            color = Widgets.InactiveColor;
            string main;
            if (IsCountGoal)
            {
                main = GameLinks.CountLabel(countThing, countCategory) + " " + CurrentCount + " / " + countTarget;
                if (GoalMet)
                {
                    main += " ✓";
                    color = MetColor;
                }
            }
            else if (link == LinkType.Event)
            {
                int last = BoardComponent.Get()?.LastEventTick(eventKind) ?? -1;
                main = last < 0
                    ? "RM_Meta_NoEventYet".Translate(GameLinks.Label(eventKind))
                    : "RM_Meta_LastEvent".Translate(GameLinks.Label(eventKind), FormatDays((Now - last) / (float)GenDate.TicksPerDay));
                if (reopenOnEvent)
                    main += " · " + "RM_Meta_Repeats".Translate();
            }
            else if (status == TicketStatus.Done)
            {
                int end = doneTick >= 0 ? doneTick : Now;
                return "RM_Meta_Took".Translate(FormatDays((end - createdTick) / (float)GenDate.TicksPerDay));
            }
            else
            {
                main = "RM_Meta_Age".Translate(FormatDays(AgeDays));
            }

            if (!HasDeadline || status == TicketStatus.Done)
                return main;
            float left = DaysLeft;
            if (left < 0f)
            {
                color = OverdueColor;
                return main + " · " + "RM_Meta_Overdue".Translate(FormatDays(-left));
            }
            if (left < 1f)
                color = WarnColor;
            return main + " · " + "RM_Meta_DueIn".Translate(FormatDays(left));
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

            Scribe_Values.Look(ref link, "link", LinkType.None);
            Scribe_Defs.Look(ref countThing, "countThing");
            Scribe_Defs.Look(ref countCategory, "countCategory");
            Scribe_Values.Look(ref countTarget, "countTarget", 30);
            Scribe_Values.Look(ref doneWhenReached, "doneWhenReached");
            Scribe_Values.Look(ref eventKind, "eventKind", EventKind.Raid);
            Scribe_Values.Look(ref reopenOnEvent, "reopenOnEvent");
        }
    }
}
