using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMinder
{
    /// <summary>Holds the board for the current save.</summary>
    public class BoardComponent : GameComponent
    {
        private const int CheckInterval = GenDate.TicksPerHour;

        public List<Ticket> tickets = new List<Ticket>();
        private int nextId = 1;
        private Dictionary<EventKind, int> lastEventTicks = new Dictionary<EventKind, int>();

        public BoardComponent(Game game)
        {
        }

        public static BoardComponent Get() => Current.Game?.GetComponent<BoardComponent>();

        public IEnumerable<Ticket> InColumn(TicketStatus status) => tickets.Where(t => t.status == status);

        public Ticket Create()
        {
            return new Ticket
            {
                id = nextId++,
                createdTick = Find.TickManager.TicksGame,
                colorIndex = 1
            };
        }

        public void Add(Ticket ticket)
        {
            if (!tickets.Contains(ticket))
                tickets.Add(ticket);
        }

        public void Remove(Ticket ticket) => tickets.Remove(ticket);

        public void SetStatus(Ticket ticket, TicketStatus status)
        {
            if (ticket.status == status)
                return;
            ticket.status = status;
            ticket.doneTick = status == TicketStatus.Done ? Find.TickManager.TicksGame : -1;
        }

        /// <summary>Moves a ticket into a column, placed before <paramref name="before"/> (or at the end when null).</summary>
        public void Move(Ticket ticket, TicketStatus status, Ticket before)
        {
            SetStatus(ticket, status);
            if (before == ticket)
                return;
            tickets.Remove(ticket);
            int index = before != null ? tickets.IndexOf(before) : -1;
            if (index < 0)
                tickets.Add(ticket);
            else
                tickets.Insert(index, ticket);
        }

        public int LastEventTick(EventKind kind) => lastEventTicks.TryGetValue(kind, out int tick) ? tick : -1;

        /// <summary>Called when an incident lands on a home map; brings repeating tickets back to Doing.</summary>
        public void RecordEvent(EventKind kind)
        {
            lastEventTicks[kind] = Find.TickManager.TicksGame;
            foreach (Ticket ticket in tickets.ToList())
            {
                if (ticket.link != LinkType.Event || !ticket.reopenOnEvent || ticket.eventKind != kind || ticket.status == TicketStatus.Doing)
                    continue;
                Move(ticket, TicketStatus.Doing, null);
                Messages.Message("RM_ReopenedMessage".Translate(ticket.title), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>For saves that had the mod added later: start event timers from the game's own history.</summary>
        private void SeedEventTicks()
        {
            foreach (EventKind kind in GameLinks.AllEventKinds)
            {
                if (lastEventTicks.ContainsKey(kind))
                    continue;
                int tick = GameLinks.TickFromStoryState(kind);
                if (tick > 0)
                    lastEventTicks[kind] = tick;
            }
        }

        public override void FinalizeInit()
        {
            SeedEventTicks();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckInterval == 0)
                CheckTickets();
        }

        /// <summary>Overdue messages and "done when reached" goals. Runs hourly; public so it can be tested.</summary>
        public void CheckTickets()
        {
            foreach (Ticket ticket in tickets)
            {
                if (ticket.IsOverdue && !ticket.overdueNotified)
                {
                    ticket.overdueNotified = true;
                    Messages.Message("RM_OverdueMessage".Translate(ticket.title), MessageTypeDefOf.NegativeEvent, false);
                }
                if (ticket.doneWhenReached && ticket.status != TicketStatus.Done && ticket.GoalMet)
                {
                    SetStatus(ticket, TicketStatus.Done);
                    Messages.Message("RM_GoalReachedMessage".Translate(ticket.title), MessageTypeDefOf.PositiveEvent, false);
                }
            }
        }

        public override void GameComponentOnGUI()
        {
            KeyBindingDef key = RimMinderDefOf.RimMinder_OpenBoard;
            if (key != null && key.KeyDownEvent)
            {
                RimMinderDefOf.ToggleBoard();
                Event.current.Use();
            }

            // OnGUI runs every frame; log a failure once instead of flooding the log.
            try
            {
                ReminderOverlay.OnGUI(this);
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[RimMinder] Reminder overlay failed: " + e, 0x4B414E01);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref tickets, "tickets", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref lastEventTicks, "lastEventTicks", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tickets ??= new List<Ticket>();
                lastEventTicks ??= new Dictionary<EventKind, int>();
            }
        }
    }
}
