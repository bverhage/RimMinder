using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyKanban
{
    /// <summary>Holds the board for the current save.</summary>
    public class KanbanGameComponent : GameComponent
    {
        private const int CheckInterval = GenDate.TicksPerHour;

        public List<Ticket> tickets = new List<Ticket>();
        private int nextId = 1;

        public KanbanGameComponent(Game game)
        {
        }

        public static KanbanGameComponent Get() => Current.Game?.GetComponent<KanbanGameComponent>();

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

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckInterval != 0)
                return;
            foreach (Ticket ticket in tickets)
            {
                if (ticket.IsOverdue && !ticket.overdueNotified)
                {
                    ticket.overdueNotified = true;
                    Messages.Message("CK_OverdueMessage".Translate(ticket.title), MessageTypeDefOf.NegativeEvent, false);
                }
            }
        }

        public override void GameComponentOnGUI()
        {
            ReminderOverlay.OnGUI(this);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref tickets, "tickets", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && tickets == null)
                tickets = new List<Ticket>();
        }
    }
}
