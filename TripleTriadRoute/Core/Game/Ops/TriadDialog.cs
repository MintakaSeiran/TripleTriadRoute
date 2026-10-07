using ECommons;
using ECommons.Automation;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Game.Ops;

// Walks an NPC's dialogue to the Triple Triad challenge: talk lines, the menu entry, and the Triple Triad yes/no.
internal static unsafe class TriadDialog
{
    private const string TalkAddon = "Talk";
    private const string SelectStringAddon = "SelectString";
    private const string SelectIconStringAddon = "SelectIconString";
    private const string SelectYesnoAddon = "SelectYesno";
    private const uint MenuListNodeId = 3;
    private const int ClickThrottleMs = 400;
    private const string TalkThrottleKey = "TripleTriadRoute.Triad.Talk";
    private const string MenuThrottleKey = "TripleTriadRoute.Triad.Menu";
    private const string YesnoThrottleKey = "TripleTriadRoute.Triad.Yesno";

    public enum Step : byte
    {
        Nothing,
        Handled,
        UnknownMenu,
        SelectedTriad,
    }

    // Only recognized challenge or cancel entries may be selected.
    public static Step Advance(bool towardChallenge = true)
    {
        if (TryGetReady(SelectYesnoAddon, out var yesno))
        {
            if (!IsOwnedByTriad(yesno))
            {
                return Step.UnknownMenu;
            }

            if (EzThrottler.Throttle(YesnoThrottleKey, ClickThrottleMs))
            {
                var prompt = new AddonMaster.SelectYesno(yesno);
                if (towardChallenge)
                {
                    prompt.Yes();
                }
                else
                {
                    prompt.No();
                }
            }

            return Step.Handled;
        }

        if (TryGetReady(SelectIconStringAddon, out var iconMenu))
        {
            return towardChallenge ? SelectTriadEntry(iconMenu, isIconMenu: true) : SelectCancelEntry(iconMenu, isIconMenu: true);
        }

        if (TryGetReady(SelectStringAddon, out var menu))
        {
            return towardChallenge ? SelectTriadEntry(menu, isIconMenu: false) : SelectCancelEntry(menu, isIconMenu: false);
        }

        if (TryGetReady(TalkAddon, out var talk))
        {
            if (EzThrottler.Throttle(TalkThrottleKey, ClickThrottleMs))
            {
                new AddonMaster.Talk(talk).Click();
            }

            return Step.Handled;
        }

        return Step.Nothing;
    }

    public static bool AnyOpen()
        => TryGetReady(TalkAddon, out _) || TryGetReady(SelectStringAddon, out _) || TryGetReady(SelectIconStringAddon, out _) || TryGetReady(SelectYesnoAddon, out _);

    private static Step SelectTriadEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var index = FindTriadEntry(menu, isIconMenu);
        if (index < 0)
        {
            return Step.UnknownMenu;
        }

        if (EzThrottler.Throttle(MenuThrottleKey, ClickThrottleMs))
        {
            RunLog.Info($"Choosing menu entry {index} ({EntryText(menu, isIconMenu, index)}).");
            Callback.Fire(menu, true, index);
            return Step.SelectedTriad;
        }

        return Step.Handled;
    }

    private static Step SelectCancelEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var snapshot = Snapshot(menu, isIconMenu);
        var index = Array.FindIndex(snapshot.Entries, TriadMenuPolicy.IsCancel);
        if (index < 0) return Step.UnknownMenu;
        if (EzThrottler.Throttle(MenuThrottleKey, ClickThrottleMs))
            Callback.Fire(menu, true, index);
        return Step.Handled;
    }

    public static TriadMenuSnapshot? CurrentMenu()
    {
        if (TryGetReady(SelectIconStringAddon, out var iconMenu)) return Snapshot(iconMenu, true);
        if (TryGetReady(SelectStringAddon, out var menu)) return Snapshot(menu, false);
        return null;
    }

    private static TriadMenuSnapshot Snapshot(AtkUnitBase* menu, bool isIconMenu)
    {
        var entries = new string[EntryCount(menu, isIconMenu)];
        var icons = new uint[entries.Length];
        var list = menu->GetComponentListById(MenuListNodeId);
        var count = list is null ? 0 : list->GetItemCount();
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = EntryText(menu, isIconMenu, index);
            if (index < count) icons[index] = list->ItemRendererList[index].IconId;
        }
        return new(isIconMenu ? SelectIconStringAddon : SelectStringAddon, entries, icons);
    }

    private static int FindTriadEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var snapshot = Snapshot(menu, isIconMenu);
        return TriadMenuPolicy.Find(snapshot.Entries, snapshot.Icons);
    }

    private static int EntryCount(AtkUnitBase* menu, bool isIconMenu)
        => isIconMenu ? new AddonMaster.SelectIconString(menu).EntryCount : new AddonMaster.SelectString(menu).EntryCount;

    private static string EntryText(AtkUnitBase* menu, bool isIconMenu, int index)
        => isIconMenu ? new AddonMaster.SelectIconString(menu).Entries[index].Text : new AddonMaster.SelectString(menu).Entries[index].Text;

    private static bool IsOwnedByTriad(AtkUnitBase* addon)
    {
        var atkModule = RaptureAtkModule.Instance();
        var agents = AgentModule.Instance();
        if (atkModule is null || agents is null || !atkModule->AddonCallbackMapping.TryGetValue(addon->Id, out var entry, false))
        {
            return false;
        }

        return entry.AgentInterface == agents->GetAgentByInternalId(AgentId.TripleTriad);
    }

    private static bool TryGetReady(string name, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(name, out addon) && GenericHelpers.IsAddonReady(addon);
}
