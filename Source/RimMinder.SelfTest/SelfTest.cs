using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMinder.SelfTest
{
    /// <summary>
    /// Runs RimMinder's in-game tests once a map is loaded, takes screenshots, writes results and (optionally) quits.
    /// Inert unless the game was started with -rimminder-selftest.
    /// </summary>
    public class SelfTestComponent : GameComponent
    {
        private const string Prefix = "[RimMinder SelfTest] ";

        private static readonly bool TestMode = HasArg("-rimminder-selftest");
        // -rimminder-screenshots=<save name>: load that save and take Steam screenshots of the real board.
        internal static readonly string ScreenshotSave = ArgValue("-rimminder-screenshots=");
        private static readonly bool Enabled = TestMode || ScreenshotSave != null;
        private static readonly bool QuitAfter = HasArg("-rimminder-quit");

        private readonly List<string> results = new List<string>();
        private readonly List<string> errors = new List<string>();
        private int passed;
        private int failed;

        private int step;
        private float nextStepTime;
        private bool listening;
        private string outDir;
        private Ticket editorTicket;

        public SelfTestComponent(Game game)
        {
        }

        private static bool HasArg(string arg) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, arg, StringComparison.OrdinalIgnoreCase));

        private static string ArgValue(string prefix)
        {
            string arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return arg?.Substring(prefix.Length).Trim('"');
        }

        public override void GameComponentUpdate()
        {
            if (!Enabled || step < 0 || Find.CurrentMap == null || Time.realtimeSinceStartup < nextStepTime)
                return;
            try
            {
                if (TestMode)
                    RunStep();
                else
                    RunScreenshotStep();
            }
            catch (Exception e)
            {
                Fail("step " + step, e.ToString());
                Finish();
            }
        }

        /// <summary>One step per call, with real-time waits in between so the UI gets frames to draw.</summary>
        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    // Let the map settle for a few seconds after loading.
                    outDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "SelfTest");
                    Directory.CreateDirectory(outDir);
                    foreach (string old in Directory.GetFiles(outDir))
                        File.Delete(old);
                    Application.logMessageReceived += OnLog;
                    listening = true;
                    Log.Message(Prefix + "starting");
                    Wait(3f);
                    break;
                case 1:
                    LogicTests();
                    Wait(1f);
                    break;
                case 2:
                    SetUpShowcase();
                    RimMinderDefOf.ToggleBoard();
                    Wait(2.5f);
                    break;
                case 3:
                    Check("ui_board_opens", Find.WindowStack.IsOpen(typeof(MainTabWindow_Board)));
                    Screenshot("1_board.png");
                    Wait(1.5f);
                    break;
                case 4:
                    Find.WindowStack.Add(new Dialog_EditTicket(editorTicket, isNew: false));
                    Wait(2f);
                    break;
                case 5:
                    Check("ui_editor_opens", Find.WindowStack.IsOpen(typeof(Dialog_EditTicket)));
                    Screenshot("2_editor.png");
                    Wait(1.5f);
                    break;
                case 6:
                    Find.WindowStack.TryRemove(typeof(Dialog_EditTicket), doCloseSound: false);
                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    Wait(2f);
                    break;
                case 7:
                    Check("ui_board_closes", !Find.WindowStack.IsOpen(typeof(MainTabWindow_Board)));
                    Screenshot("3_overlay.png");
                    Wait(2f);
                    break;
                default:
                    Finish();
                    return;
            }
            step++;
        }

        private void Wait(float seconds) => nextStepTime = Time.realtimeSinceStartup + seconds;

        // ---------------------------------------------------------------- Steam screenshots of a real save

        /// <summary>Screenshots of the player's own board. Never changes the colony and never saves.</summary>
        private void RunScreenshotStep()
        {
            BoardComponent board = BoardComponent.Get();
            switch (step)
            {
                case 0:
                    outDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "Screenshots");
                    Directory.CreateDirectory(outDir);
                    Application.logMessageReceived += OnLog;
                    listening = true;
                    Wait(6f);
                    break;
                case 1:
                    Find.TickManager.Pause();
                    Messages.Clear();
                    Find.PlaySettings.showLearningHelper = false;
                    // The debug log can pop up on unrelated startup warnings; keep it out of the shots.
                    Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), doCloseSound: false);
                    RimMinderMod.Settings.showOverlay = true;
                    // Show the default UI, whatever the tester left in the settings (not written to disk).
                    RimMinderMod.Settings.showButton = true;
                    RimMinderMod.Settings.ApplyButtonVisibility();
                    FocusCameraOnColony();
                    Wait(2.5f);
                    break;
                case 2:
                    Screenshot("1_overlay.png");
                    Wait(1f);
                    break;
                case 3:
                    RimMinderMod.Settings.boardSize = new Vector2(820f, 430f);
                    RimMinderDefOf.ToggleBoard();
                    Wait(2.5f);
                    break;
                case 4:
                    Screenshot("2_board.png");
                    Wait(1f);
                    break;
                case 5:
                    editorTicket = board?.tickets.FirstOrDefault(t => t.link != LinkType.None && t.status != TicketStatus.Done)
                        ?? board?.tickets.FirstOrDefault();
                    if (editorTicket != null)
                        Find.WindowStack.Add(new Dialog_EditTicket(editorTicket, isNew: false));
                    Wait(2.5f);
                    break;
                case 6:
                    Screenshot("3_editor.png");
                    Wait(1.5f);
                    break;
                default:
                    Finish();
                    return;
            }
            step++;
        }

        private static void FocusCameraOnColony()
        {
            Map map = Find.CurrentMap;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            Vector3 center = colonists.Count > 0
                ? colonists.Aggregate(Vector3.zero, (sum, p) => sum + p.DrawPos) / colonists.Count
                : map.Center.ToVector3Shifted();
            Find.CameraDriver.SetRootPosAndSize(center, 26f);
        }

        // ---------------------------------------------------------------- logic tests

        private void LogicTests()
        {
            BoardComponent board = BoardComponent.Get();
            Check("component_exists", board != null);
            if (board == null)
                return;
            Map map = Find.CurrentMap;
            int now = Find.TickManager.TicksGame;

            Check("harmony_patch_incidents", HasOurPatch(AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))));
            Check("harmony_patch_playsettings", HasOurPatch(AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))));
            Check("def_main_button", RimMinderDefOf.RimMinder_Plans != null);
            Check("def_keybinding", RimMinderDefOf.RimMinder_OpenBoard != null);
            Check("texture_toggle", RimMinderTex.OverlayToggle != null && RimMinderTex.OverlayToggle != BaseContent.BadTex);
            Check("translation_keys", "RM_BoardTitle".CanTranslate() && "RM_Link_Count".CanTranslate() && "RM_Event_Raid".CanTranslate());

            // Create, add, move between columns.
            int before = board.tickets.Count;
            Ticket a = NewTicket(board, "test A");
            Ticket b = NewTicket(board, "test B");
            Check("create_add", board.tickets.Count == before + 2);
            board.SetStatus(a, TicketStatus.Doing);
            Check("move_to_doing", a.status == TicketStatus.Doing && a.doneTick == -1);
            board.SetStatus(a, TicketStatus.Done);
            Check("move_to_done_sets_tick", a.status == TicketStatus.Done && a.doneTick == now);
            board.Move(a, TicketStatus.Plan, b);
            Check("move_back_and_reorder", a.status == TicketStatus.Plan && a.doneTick == -1 && board.tickets.IndexOf(a) < board.tickets.IndexOf(b));

            // Deadlines.
            a.deadlineTick = now - 1;
            Check("deadline_overdue", a.IsOverdue);
            board.CheckTickets();
            Check("overdue_notified_once", a.overdueNotified);
            Check("meta_label_overdue", a.MetaLabel(out Color c).Length > 0 && c == Ticket.OverdueColor);
            a.deadlineTick = now + GenDate.TicksPerDay * 3;
            Check("deadline_not_overdue", !a.IsOverdue);

            // Count goals: put steel in a fresh stockpile and compare with the game's own counter.
            int steelBefore = map.resourceCounter.GetCount(ThingDefOf.Steel);
            int spawned = SpawnSteelInStockpile(map, 2);
            map.resourceCounter.UpdateResourceCounts();
            int steelAfter = map.resourceCounter.GetCount(ThingDefOf.Steel);
            Check("count_spawned_in_storage", spawned > 0 && steelAfter - steelBefore == spawned,
                "before " + steelBefore + ", spawned " + spawned + ", after " + steelAfter);
            Ticket goal = NewTicket(board, "test goal");
            goal.link = LinkType.Count;
            goal.countThing = ThingDefOf.Steel;
            goal.countTarget = steelAfter + 1;
            Check("count_matches_counter", goal.CurrentCount == steelAfter, goal.CurrentCount + " vs " + steelAfter);
            Check("count_goal_not_met", !goal.GoalMet);
            Ticket goalMet = NewTicket(board, "test goal met");
            goalMet.link = LinkType.Count;
            goalMet.countThing = ThingDefOf.Steel;
            goalMet.countTarget = Math.Max(1, steelAfter);
            goalMet.doneWhenReached = true;
            board.CheckTickets();
            Check("count_goal_done_when_reached", goalMet.GoalMet && goalMet.status == TicketStatus.Done);
            ThingCategoryDef meals = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
            Check("count_category_meals", meals != null && GameLinks.Count(null, meals) == map.resourceCounter.GetCountIn(meals));

            // Events: fire a real raid and check the Harmony hook recorded it and reopened the right ticket.
            Ticket repeat = NewTicket(board, "test repeat on raid");
            repeat.link = LinkType.Event;
            repeat.eventKind = EventKind.Raid;
            repeat.reopenOnEvent = true;
            board.SetStatus(repeat, TicketStatus.Done);
            Ticket otherKind = NewTicket(board, "test repeat on trader");
            otherKind.link = LinkType.Event;
            otherKind.eventKind = EventKind.TraderCaravan;
            otherKind.reopenOnEvent = true;
            board.SetStatus(otherKind, TicketStatus.Done);

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            bool raided = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            if (raided)
            {
                Check("event_raid_recorded", board.LastEventTick(EventKind.Raid) == now);
                Check("event_bigthreat_recorded", board.LastEventTick(EventKind.BigThreat) == now);
                Check("event_reopens_ticket", repeat.status == TicketStatus.Doing);
                Check("event_other_kind_untouched", otherKind.status == TicketStatus.Done);
                Check("event_meta_label", repeat.MetaLabel(out _).Contains("0.0"), repeat.MetaLabel(out _));
            }
            else
            {
                Skip("event tests", "the raid incident could not fire on this map");
            }

            // Save and load: write tickets with the game's own Scribe system and read them back.
            ScribeRoundTrip(new List<Ticket> { a, goal, repeat });

            // Clean up the test tickets.
            board.tickets.RemoveAll(t => t.title.StartsWith("test "));
            Check("cleanup", board.tickets.All(t => !t.title.StartsWith("test ")));
        }

        private static Ticket NewTicket(BoardComponent board, string title)
        {
            Ticket t = board.Create();
            t.title = title;
            board.Add(t);
            return t;
        }

        private static bool HasOurPatch(System.Reflection.MethodBase method)
        {
            Patches info = Harmony.GetPatchInfo(method);
            return info != null && info.Owners.Contains("theB52.RimMinder");
        }

        private static int SpawnSteelInStockpile(Map map, int stacks)
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            int spawned = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, 12f, true))
            {
                if (stacks == 0)
                    break;
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstItem(map) != null || map.zoneManager.ZoneAt(cell) != null)
                    continue;
                zone.AddCell(cell);
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = steel.def.stackLimit;
                GenSpawn.Spawn(steel, cell, map);
                spawned += steel.stackCount;
                stacks--;
            }
            return spawned;
        }

        private void ScribeRoundTrip(List<Ticket> original)
        {
            string path = Path.Combine(outDir, "scribe_roundtrip.xml");
            List<Ticket> toSave = original.ToList();
            Scribe.saver.InitSaving(path, "rimminderSelfTest");
            Scribe_Collections.Look(ref toSave, "tickets", LookMode.Deep);
            Scribe.saver.FinalizeSaving();

            List<Ticket> loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Collections.Look(ref loaded, "tickets", LookMode.Deep);
            Scribe.loader.FinalizeLoading();

            bool same = loaded != null && loaded.Count == original.Count;
            for (int i = 0; same && i < original.Count; i++)
            {
                Ticket x = original[i], y = loaded[i];
                same = x.id == y.id && x.title == y.title && x.status == y.status && x.colorIndex == y.colorIndex
                    && x.createdTick == y.createdTick && x.deadlineTick == y.deadlineTick && x.link == y.link
                    && x.countThing == y.countThing && x.countCategory == y.countCategory && x.countTarget == y.countTarget
                    && x.eventKind == y.eventKind && x.reopenOnEvent == y.reopenOnEvent && x.doneWhenReached == y.doneWhenReached;
            }
            Check("save_load_roundtrip", same);
        }

        // ---------------------------------------------------------------- screenshots

        /// <summary>A realistic board for the screenshots (also useful as Steam preview material).</summary>
        private void SetUpShowcase()
        {
            BoardComponent board = BoardComponent.Get();
            int now = Find.TickManager.TicksGame;
            int day = GenDate.TicksPerDay;
            board.tickets.Clear();

            Ticket Add(string title, TicketStatus status, int color, float ageDays)
            {
                Ticket t = board.Create();
                t.title = title;
                t.colorIndex = color;
                t.createdTick = now - (int)(ageDays * day);
                board.Add(t);
                board.SetStatus(t, status);
                return t;
            }

            Add("Build a freezer", TicketStatus.Plan, 2, 2.0f);
            Ticket meals = Add("Keep the freezer stocked", TicketStatus.Plan, 2, 1.2f);
            meals.link = LinkType.Count;
            meals.countCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
            meals.countTarget = 30;
            Ticket steel = Add("Stockpile steel for the wall", TicketStatus.Plan, 0, 0.5f);
            steel.link = LinkType.Count;
            steel.countThing = ThingDefOf.Steel;
            steel.countTarget = 300;
            editorTicket = steel;

            Ticket wing = Add("Finish the blue bedroom wing", TicketStatus.Doing, 1, 3.0f);
            wing.deadlineTick = now + (int)(0.6f * day);
            Ticket research = Add("Research hydroponics", TicketStatus.Doing, 5, 4.2f);
            // A quick-test colony is only hours old, so keep the deadline at or after tick 0 (-1 means "no deadline").
            research.deadlineTick = Math.Max(0, now - (int)(0.3f * day));

            Ticket walls = Add("Repair walls after a raid", TicketStatus.Done, 4, 6.0f);
            walls.link = LinkType.Event;
            walls.eventKind = EventKind.Raid;
            walls.reopenOnEvent = true;
            Add("Wall in the base", TicketStatus.Done, 0, 5.0f);

            RimMinderMod.Settings.showOverlay = true;
            Find.TickManager.Pause();
            // Clean screenshots: no leftover messages from the logic tests, no learning helper.
            Messages.Clear();
            Find.PlaySettings.showLearningHelper = false;
        }

        private void Screenshot(string name)
        {
            string path = Path.Combine(outDir, name);
            ScreenCapture.CaptureScreenshot(path);
            results.Add("SHOT " + name);
        }

        // ---------------------------------------------------------------- reporting

        private void Check(string name, bool ok, string detail = null)
        {
            if (ok)
            {
                passed++;
                results.Add("PASS " + name);
            }
            else
            {
                Fail(name, detail);
            }
        }

        private void Fail(string name, string detail)
        {
            failed++;
            results.Add("FAIL " + name + (detail != null ? ": " + detail : ""));
        }

        private void Skip(string name, string reason) => results.Add("SKIP " + name + ": " + reason);

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                errors.Add(message + (type == LogType.Exception ? "\n" + stackTrace : ""));
        }

        private void Finish()
        {
            step = -1;
            if (listening)
            {
                Application.logMessageReceived -= OnLog;
                listening = false;
            }
            Check("no_errors_logged_during_test", errors.Count == 0, errors.Count + " error(s), see below");

            var sb = new StringBuilder();
            sb.AppendLine("RimMinder self-test: " + passed + " passed, " + failed + " failed");
            foreach (string line in results)
                sb.AppendLine(line);
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Errors logged during the test:");
                foreach (string e in errors.Take(20))
                    sb.AppendLine("- " + e);
            }
            string report = sb.ToString();
            if (outDir != null)
                File.WriteAllText(Path.Combine(outDir, "results.txt"), report);
            Log.Message(Prefix + report);

            if (QuitAfter)
                LongEventHandler.ExecuteWhenFinished(Application.Quit);
        }
    }
}

namespace RimMinder.SelfTest
{
    /// <summary>In screenshot mode, loads the named save as soon as the game reaches the main menu.</summary>
    [StaticConstructorOnStartup]
    public static class ScreenshotSaveLoader
    {
        static ScreenshotSaveLoader()
        {
            string save = SelfTestComponent.ScreenshotSave;
            if (save.NullOrEmpty())
                return;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Log.Message("[RimMinder SelfTest] loading save for screenshots: " + save);
                GameDataSaveLoader.LoadGame(save);
            });
        }
    }
}
