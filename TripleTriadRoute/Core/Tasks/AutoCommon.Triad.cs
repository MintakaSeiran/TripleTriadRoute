using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Addons;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Decks;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Match;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

internal enum NpcRunEnd : byte
{
    GoalMet,
    Skipped,
    StopRun,
}

internal readonly record struct NpcRunResult(NpcRunEnd End, SkipReason Reason);

// Per-NPC state that must not outlive the NPC: the deck in play, the screen tracker and the streak counters.
internal sealed class TriadNpcRun(ushort npcIndex, Func<TriadNpcRun, bool> goalMet)
{
    public readonly ushort NpcIndex = npcIndex;
    public readonly Func<TriadNpcRun, bool> GoalMet = goalMet;
    public readonly TriadDeck NpcDeck = TriadDeckBuilder.NpcDeck(npcIndex);
    public readonly TriadScreenMemory Memory = new();
    public readonly TriadScreenState Screen = new();
    public readonly TriadRuleId[] Regional = new TriadRuleId[2];
    public int Matches;
    public int LossStreak;
    public int InteractFailures;
    public int FailedSeries;
    public SkipReason ChallengeFailure;
    public readonly TriadAvailabilityCheck Availability = new();
    public ushort DeckRuleMask = ushort.MaxValue;
    public bool DeckReady;
    // Zero plays on until the goal is met; Collect sets it from the per-NPC match limit.
    public int MatchLimit;

    public bool MatchLimitReached => MatchLimit > 0 && Matches >= MatchLimit;

    public TriadNpc Npc => TriadData.Set.Npcs[NpcIndex];

    public string Name => TriadData.Set.NpcNames[NpcIndex];
}

public abstract partial class AutoCommon
{
    private const float InteractRangeMeters = 4f;
    // Travel counts a stop a couple of metres past its tolerance as arrived, which can leave the NPC out of talking range.
    private const float TalkRangeMeters = 3f;
    private const float TalkApproachToleranceMeters = 1.5f;
    private const int TalkApproachWatchdogMs = 10_000;
    private const int MaxInteractFailures = 6;
    private const int ChallengeOpenTimeoutMs = 15_000;
    private const int DialogSettleTimeoutMs = 8_000;
    private const int MaxFailedSeries = 3;

    // Card items that would not register this run, so each is tried once rather than before every match.
    private readonly HashSet<uint> unregistrableItems = [];

    internal async Task<NpcRunResult> PlayNpc(TriadNpcRun run, AutoTriadSession session, TriadProgress progress)
    {
        var configuration = Plugin.Instance.Configuration;
        progress.BeginNpc(run.NpcIndex);
        LoadRegionalRules(configuration, run);
        PrepareDeck(configuration, run);

        // A menu left for manual dismissal must never be attributed to the next NPC.
        if (TriadDialog.AnyOpen() && !TriadAddons.AnyMatchWindowVisible())
            return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.MenuRecognitionFailed);

        progress.SetPhase(TriadPhase.Travelling);
        var npc = run.Npc;
        if (!await ReachNpc(run))
        {
            return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.Unreachable);
        }

        while (!CancelToken.IsCancellationRequested)
        {
            TriadOwnership.Refresh(force: true);
            if (run.GoalMet(run))
            {
                await LeaveMatchWindows();
                return new NpcRunResult(NpcRunEnd.GoalMet, SkipReason.None);
            }

            if (Stop(configuration, run) is { } limit)
            {
                await LeaveMatchWindows();
                return new NpcRunResult(NpcRunEnd.Skipped, limit);
            }

            progress.SetPhase(TriadPhase.Registering);
            await RegisterPendingCards(session);
            if (CardRegistrar.FreeBagSlots() < configuration.MinFreeBagSlots)
            {
                Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Your bags are nearly full, so the run stops. Free a few slots and start again.");
                return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.InventoryFull);
            }

            if (CardRegistrar.Mgp() < npc.Fee)
            {
                Svc.Chat.PrintError($"{AttgConstants.LogPrefix} You need {npc.Fee} MGP to challenge {run.Name}, so the run stops.");
                return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.NotEnoughMgp);
            }

            progress.SetPhase(TriadPhase.Challenging);
            if (!await OpenChallenge(run))
            {
                if (CancelToken.IsCancellationRequested)
                    return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.None);
                if (run.ChallengeFailure != SkipReason.None)
                    return new NpcRunResult(NpcRunEnd.Skipped, run.ChallengeFailure);
                run.InteractFailures++;
                if (run.InteractFailures >= MaxInteractFailures)
                {
                    return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.InteractFailed);
                }

                await DelayMs(1_000);
                continue;
            }

            run.InteractFailures = 0;
            var matchesBefore = run.Matches;
            var leave = await PlayMatchSeries(configuration, run, session, progress);
            await SettleDialog();
            if (leave is { } stop)
            {
                return stop;
            }

            run.FailedSeries = run.Matches == matchesBefore ? run.FailedSeries + 1 : 0;
            if (run.FailedSeries >= MaxFailedSeries)
            {
                return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.InteractFailed);
            }
        }

        return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.None);
    }

    private static SkipReason? Stop(Configuration configuration, TriadNpcRun run)
    {
        if (configuration.LossStreakSkip > 0 && run.LossStreak >= configuration.LossStreakSkip)
        {
            return SkipReason.LossStreak;
        }

        return null;
    }

    private static void LoadRegionalRules(Configuration configuration, TriadNpcRun run)
    {
        if (!configuration.RegionalRules.TryGetValue(run.Npc.TriadRowId, out var remembered))
        {
            return;
        }

        for (var index = 0; index < run.Regional.Length && index < remembered.Length; index++)
        {
            run.Regional[index] = remembered[index];
        }
    }

    // Starts the build before travel, so it usually finishes on the way.
    private static void PrepareDeck(Configuration configuration, TriadNpcRun run)
    {
        if (configuration.DeckSource != DeckSource.Optimized)
        {
            run.DeckReady = true;
            return;
        }

        var ruleMask = TriadDeckBuilder.RuleMask(run.NpcIndex, run.Regional);
        run.DeckRuleMask = ruleMask;
        if (TriadDeckBuilder.TryGetCached(configuration, run.NpcIndex, ruleMask, out _))
        {
            run.DeckReady = true;
            return;
        }

        run.DeckReady = false;
        TriadDeckBuilder.Start(configuration, run.NpcIndex, ruleMask);
    }

    private async Task<bool> ReachNpc(TriadNpcRun run)
    {
        var npc = run.Npc;
        if (NpcInteraction.FindNearest(npc.ENpcBaseId) is { } nearby && Svc.ClientState.TerritoryType == npc.TerritoryId
            && WithinReach(nearby.Position, InteractRangeMeters))
        {
            return true;
        }

        if (!await TravelTo(npc.TerritoryId, npc.Position, InteractRangeMeters))
        {
            Warn($"Could not reach {run.Name} in territory {npc.TerritoryId}.");
            return false;
        }

        if (NpcInteraction.FindNearest(npc.ENpcBaseId) is { } found && !WithinReach(found.Position, InteractRangeMeters))
        {
            await TravelTo(npc.TerritoryId, found.Position, InteractRangeMeters);
        }

        await SafeDismount("triad-npc");
        return NpcInteraction.FindNearest(npc.ENpcBaseId) is not null;
    }

    // Talks to the NPC and walks the dialogue until the challenge window opens.
    private async Task<bool> OpenChallenge(TriadNpcRun run)
    {
        if (TriadAddons.AnyMatchWindowVisible())
        {
            return true;
        }

        var deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
        var lastInteract = 0L;
        var interacted = false;
        var sawTalk = false;
        var sawSelection = false;
        var dialogueClosedAt = 0L;
        var availability = run.Availability;
        run.ChallengeFailure = SkipReason.None;
        var approached = false;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.IsVisible(TriadAddons.Request) || TriadAddons.IsVisible(TriadAddons.DeckSelect))
            {
                return true;
            }

            // Only inspect dialogue after this operation interacted with the intended NPC.
            if (!interacted && TriadDialog.AnyOpen())
            {
                run.ChallengeFailure = SkipReason.MenuRecognitionFailed;
                LogMissingMenu(run, TriadDialog.CurrentMenu(), 1, run.ChallengeFailure);
                return false;
            }
            var step = interacted ? TriadDialog.Advance() : TriadDialog.Step.Nothing;
            if (step == TriadDialog.Step.TalkHandled) sawTalk = true;
            if (step is TriadDialog.Step.Handled or TriadDialog.Step.SelectedTriad or TriadDialog.Step.UnknownMenu)
                sawSelection = true;
            if (step != TriadDialog.Step.Nothing) dialogueClosedAt = 0;
            if (step == TriadDialog.Step.SelectedTriad) availability.SelectedTriad = true;
            if (step == TriadDialog.Step.Nothing && interacted && sawTalk && !sawSelection && NpcInteraction.PlayerReady())
            {
                // Wait for delayed menus before counting a completed, Talk-only interaction.
                if (dialogueClosedAt == 0) dialogueClosedAt = Environment.TickCount64;
                if (Environment.TickCount64 - dialogueClosedAt < 750)
                {
                    await NextFrame(5);
                    continue;
                }
                var reason = availability.ObserveTalkOnly();
                Warn($"[TripleTriadRoute] {run.Name} (ENpcBaseId {run.Npc.ENpcBaseId}, TriadRowId {run.Npc.TriadRowId}, territory {Svc.ClientState.TerritoryType}): Talk closed without a challenge/menu. Attempt {availability.Attempts}/2; {reason}.");
                if (reason != SkipReason.None)
                {
                    run.ChallengeFailure = reason;
                    return false;
                }
                interacted = false;
                sawTalk = sawSelection = false;
                dialogueClosedAt = 0;
                lastInteract = 0;
                deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
            }
            if (step == TriadDialog.Step.UnknownMenu)
            {
                var menu = TriadDialog.CurrentMenu();
                var reason = availability.Observe(menu);
                LogMissingMenu(run, menu, availability.Attempts, reason);
                var closed = await CloseObservedMenu(menu);
                if (!closed)
                {
                    run.ChallengeFailure = reason == SkipReason.InteractFailed ? reason : SkipReason.MenuRecognitionFailed;
                    Warn($"[TripleTriadRoute] Could not safely close {run.Name}'s menu. Skipping with {run.ChallengeFailure}; manual dismissal required.");
                    return false;
                }
                if (reason == SkipReason.InteractFailed) return false;
                if (reason != SkipReason.None)
                {
                    run.ChallengeFailure = reason;
                    return false;
                }
                await DelayMs(750);
                interacted = false;
                sawTalk = sawSelection = false;
                dialogueClosedAt = 0;
                approached = false;
                lastInteract = 0;
                deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
                continue;
            }

            if (step == TriadDialog.Step.Nothing && NpcInteraction.PlayerReady() && Environment.TickCount64 - lastInteract > 1_500
                && NpcInteraction.FindNearest(run.Npc.ENpcBaseId) is { } target)
            {
                if (!approached && await ApproachToTalk(run, target))
                {
                    approached = true;
                    deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
                    continue;
                }

                NpcInteraction.Target(target);
                NpcInteraction.Interact(target);
                lastInteract = Environment.TickCount64;
                interacted = true;
            }

            await NextFrame(5);
        }

        Warn($"The challenge window for {run.Name} did not open.");
        // Reopen a clean dialogue on the existing interaction retry budget.
        if (TriadDialog.AnyOpen() && !await CloseObservedMenu(TriadDialog.CurrentMenu()))
            run.ChallengeFailure = availability.SelectedTriad ? SkipReason.InteractFailed : SkipReason.MenuRecognitionFailed;
        return false;
    }

    private void LogMissingMenu(TriadNpcRun run, TriadMenuSnapshot? menu, int attempt, SkipReason reason)
    {
        var entries = menu is null ? "<no readable selection menu; unknown prompt>" :
            string.Join("\n", menu.Entries.Select((text, index) => $"[{index}] {text} (icon {menu.Icons[index]})"));
        var keywords = menu is null ? "" : string.Join(", ", menu.Entries.Select(TriadMenuPolicy.Keyword).Where(word => word is not null));
        Warn($"[TripleTriadRoute] Triple Triad entry not found.\nNPC: {run.Name}\nENpcBaseId: {run.Npc.ENpcBaseId}\nTriadRowId: {run.Npc.TriadRowId}\nTerritory: {Svc.ClientState.TerritoryType}\nAttempt: {attempt}/2\nReason: {reason}\nDetected keyword: {keywords}\nMenu entries:\n{entries}\nAction: {(reason == SkipReason.None ? "Cancel safely and recheck." : "Skip this NPC for this session; never select unknown entries.")}");
    }

    private async Task<bool> CloseObservedMenu(TriadMenuSnapshot? observed)
    {
        if (observed is null || !observed.Entries.Any(TriadMenuPolicy.IsCancel)) return false;
        var deadline = Environment.TickCount64 + 3_000;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (!TriadDialog.AnyOpen()) return true;
            var current = TriadDialog.CurrentMenu();
            if (current?.Fingerprint != observed.Fingerprint) return false;
            // Advance(false) now only selects a positively identified cancel item.
            TriadDialog.Advance(towardChallenge: false);
            await NextFrame(5);
        }
        return false;
    }

    private async Task<bool> ApproachToTalk(TriadNpcRun run, IGameObject target)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var distance = Vector3.Distance(player.Position, target.Position);
        if (distance <= TalkRangeMeters)
        {
            return false;
        }

        Diag($"Walking up to {run.Name}, {distance:F1}m away.");
        var approach = new MoveOp(move => move.MoveInZone(target.Position, walkMovement.WithTolerance(TalkApproachToleranceMeters), null));
        await RunCancellable(approach, TalkApproachWatchdogMs, "triad-approach", StuckDetector.MoveStallAbort("triad-approach"));
        if (approach.Fault is { } fault)
        {
            Diag($"The walk up to {run.Name} faulted: {fault.Message}");
        }

        return true;
    }

    private async Task SettleDialog()
    {
        var deadline = Environment.TickCount64 + DialogSettleTimeoutMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.AnyMatchWindowVisible())
            {
                TriadMatchOps.CloseOneWindow();
            }
            else if (TriadDialog.Advance(towardChallenge: false) == TriadDialog.Step.Nothing)
            {
                return;
            }

            await NextFrame(5);
        }
    }

    private async Task RegisterPendingCards(AutoTriadSession session)
    {
        for (var attempt = 0; attempt < 20 && !CancelToken.IsCancellationRequested; attempt++)
        {
            var itemId = CardRegistrar.FindUnregisteredCardItem(unregistrableItems);
            if (itemId == 0 || !TriadData.Set.CardIdByItemId.TryGetValue(itemId, out var cardId))
            {
                return;
            }

            if (!await WaitUntilTimed(NpcInteraction.PlayerReady, 10_000, "register-ready"))
            {
                return;
            }

            var cardName = TriadData.Set.CardName(cardId);
            Diag($"Registering {cardName} (item {itemId}).");
            CardRegistrar.Use(itemId);
            var registered = await WaitUntilTimed(() =>
            {
                // Only a prompt about this card is confirmed; anything else is left for the player.
                if (NpcInteraction.SelectYesnoOpen() && NpcInteraction.SelectYesnoText().Contains(cardName, StringComparison.OrdinalIgnoreCase))
                {
                    DialogDriver.Confirm();
                }

                TriadOwnership.Refresh(force: true);
                return TriadOwnership.IsOwned(cardId);
            }, 8_000, "register-card");
            if (!registered)
            {
                Warn($"{cardName} did not register; leaving it in your bags.");
                unregistrableItems.Add(itemId);
                continue;
            }

            session.RecordCardRegistered();
            session.RecordCardObtained(cardId);
            await DelayMs(600);
        }
    }
}
