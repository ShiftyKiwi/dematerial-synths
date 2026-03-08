using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using static DEMATSYNTH.Enums.DMSState;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;
using AddonCallback = ECommons.Automation.Callback;
using ECommonsSalvageResult = ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.SalvageResult;
using Item = Lumina.Excel.Sheets.Item;

namespace DEMATSYNTH.Scheduler;

internal static unsafe class SchedulerMain
{
    private static readonly TimeSpan DialogOpenTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan UiActionThrottle = TimeSpan.FromMilliseconds(500);

    internal static bool DebugOOMMain = false;
    internal static bool DebugOOMSub = false;

    internal static DMSState State { get; private set; } = Idle;
    internal static string StatusMessage { get; private set; } = "Idle.";
    internal static string LastResult { get; private set; } = "Nothing has run yet.";
    internal static string TargetName { get; private set; } = "None";
    internal static bool IsBusy => State != Idle;

    private static bool shouldRetrieve;
    private static bool shouldDesynth;
    private static GameInventoryType targetContainerType;
    private static int targetSlot;
    private static uint targetBaseItemId;
    private static DateTime stateStartedAtUtc;
    private static DateTime lastUiActionAtUtc;

    internal static bool Start(GameInventoryItem item, string targetName, bool retrieveMateria, bool desynth)
    {
        if (IsBusy)
        {
            ReportChat($"Already working on {TargetName}. Stop the current run first.");
            return false;
        }

        if (item.IsEmpty)
        {
            ReportChat("The selected item is no longer available.");
            return false;
        }

        shouldRetrieve = retrieveMateria;
        shouldDesynth = desynth;
        targetContainerType = item.ContainerType;
        targetSlot = (int)item.InventorySlot;
        targetBaseItemId = item.BaseItemId;
        TargetName = string.IsNullOrWhiteSpace(targetName) ? ResolveItemName(item) : targetName;
        lastUiActionAtUtc = DateTime.MinValue;

        LastResult = $"Working on {TargetName}.";
        ReportChat($"Starting {BuildOperationDescription()} for {TargetName}.");

        if (shouldRetrieve)
        {
            SetState(WaitingForRetrieveDialog, $"Waiting for Retrieve Materia for {TargetName}...");
            return true;
        }

        return TryOpenDesynthesis();
    }

    internal static bool DisablePlugin()
    {
        return DisablePlugin("Stopped.");
    }

    internal static bool DisablePlugin(string reason)
    {
        State = Idle;
        StatusMessage = reason;
        LastResult = reason;
        shouldRetrieve = false;
        shouldDesynth = false;
        lastUiActionAtUtc = DateTime.MinValue;
        return true;
    }

    internal static void Tick()
    {
        if (!Throttles.GenericThrottle || State == Idle)
        {
            return;
        }

        if (Svc.Objects.LocalPlayer == null)
        {
            DisablePlugin("Player unavailable.");
            return;
        }

        switch (State)
        {
            case WaitingForRetrieveDialog:
                TickWaitingForRetrieveDialog();
                break;
            case WaitingForRetrieveCompletion:
                TickWaitingForRetrieveCompletion();
                break;
            case WaitingForDesynthDialog:
                TickWaitingForDesynthDialog();
                break;
            case WaitingForDesynthResult:
                TickWaitingForDesynthResult();
                break;
            default:
                DisablePlugin("Unknown scheduler state.");
                break;
        }
    }

    internal static bool CanRetrieveMateria(GameInventoryItem item)
    {
        return !item.IsEmpty && item.MateriaEntries.Count > 0;
    }

    private static void TickWaitingForRetrieveDialog()
    {
        if (GenericHelpers.TryGetAddonMaster<MateriaRetrieveDialog>(out var dialog) && dialog.IsAddonReady)
        {
            if (P.Config.AutoConfirmRetrieveMateria)
            {
                dialog.Begin();
                SetState(WaitingForRetrieveCompletion, $"Retrieving materia from {TargetName}...");
            }
            else
            {
                SetState(WaitingForRetrieveCompletion, $"Waiting for you to confirm Retrieve Materia for {TargetName}...");
            }

            return;
        }

        if (!GenericHelpers.IsOccupied() && TryGetLiveTargetItem(out var item) && CanRetrieveMateria(item))
        {
            if (TrySelectNextMateria())
            {
                return;
            }
        }

        if (HasTimedOut(DialogOpenTimeout))
        {
            Fail("The Retrieve Materia dialog did not open.");
        }
    }

    private static void TickWaitingForRetrieveCompletion()
    {
        if (GenericHelpers.TryGetAddonMaster<MateriaRetrieveDialog>(out var dialog) && dialog.IsAddonReady)
        {
            if (P.Config.AutoConfirmRetrieveMateria)
            {
                dialog.Begin();
            }

            return;
        }

        if (GenericHelpers.IsOccupied())
        {
            return;
        }

        if (!TryGetLiveTargetItem(out var item))
        {
            Fail("The target item is no longer available.");
            return;
        }

        if (CanRetrieveMateria(item))
        {
            if (TrySelectNextMateria())
            {
                return;
            }

            if (HasTimedOut(ActionTimeout))
            {
                Fail("Materia retrieval did not finish.");
            }

            return;
        }

        if (TryCloseRetrieveWindow())
        {
            return;
        }

        if (shouldDesynth)
        {
            TryOpenDesynthesis();
            return;
        }

        Complete($"Retrieved materia from {TargetName}.");
    }

    private static void TickWaitingForDesynthDialog()
    {
        if (GenericHelpers.TryGetAddonMaster<SalvageDialog>(out var dialog) && dialog.IsAddonReady)
        {
            if (P.Config.AutoConfirmDesynthesis)
            {
                dialog.Desynthesize();
                SetState(WaitingForDesynthResult, $"Desynthesizing {TargetName}...");
            }
            else
            {
                SetState(WaitingForDesynthResult, $"Waiting for you to confirm desynthesis for {TargetName}...");
            }

            return;
        }

        if (HasTimedOut(DialogOpenTimeout))
        {
            Fail("The desynthesis confirmation did not open.");
        }
    }

    private static void TickWaitingForDesynthResult()
    {
        if (GenericHelpers.TryGetAddonMaster<SalvageDialog>(out var dialog) && dialog.IsAddonReady)
        {
            if (P.Config.AutoConfirmDesynthesis)
            {
                dialog.Desynthesize();
            }

            return;
        }

        if (GenericHelpers.TryGetAddonMaster<ECommonsSalvageResult>(out var result) && result.IsAddonReady)
        {
            if (P.Config.AutoCloseDesynthesisResult)
            {
                result.Close();
            }

            Complete($"Desynthesized {TargetName}.");
            return;
        }

        if (GenericHelpers.TryGetAddonMaster<SalvageAutoDialog>(out var autoDialog) && autoDialog.IsAddonReady && autoDialog.DesynthesisInactive)
        {
            if (P.Config.AutoCloseDesynthesisResult)
            {
                autoDialog.EndDesynthesis();
            }

            Complete($"Desynthesized {TargetName}.");
            return;
        }

        if (!GenericHelpers.IsOccupied() && !TryGetLiveTargetItem(out _))
        {
            Complete($"Desynthesized {TargetName}.");
            return;
        }

        if (HasTimedOut(ActionTimeout))
        {
            Fail("Desynthesis did not finish.");
        }
    }

    private static bool TryOpenDesynthesis()
    {
        if (!shouldDesynth)
        {
            Complete($"Finished working on {TargetName}.");
            return true;
        }

        if (!TryGetLiveTargetItem(out var item))
        {
            Fail("The target item is no longer available.");
            return false;
        }

        if (!TryGetInventoryItemPointer(item, out var inventoryItem))
        {
            Fail("The target item no longer has a valid inventory address.");
            return false;
        }

        var salvageAgent = GetSalvageAgent();
        if (salvageAgent == null)
        {
            Fail("Could not access the desynthesis agent.");
            return false;
        }

        salvageAgent->SalvageItem(inventoryItem);
        SetState(WaitingForDesynthDialog, $"Waiting for the desynthesis confirmation for {TargetName}...");
        return true;
    }

    private static bool TrySelectNextMateria()
    {
        if (!CanIssueUiAction() || !TryGetMaterializeAddon(out var addon))
        {
            return false;
        }

        AddonCallback.Fire(addon, true, 2, 0);
        lastUiActionAtUtc = DateTime.UtcNow;
        StatusMessage = $"Selecting the next materia for {TargetName}...";
        LoggingUtil.Debug(StatusMessage);
        return true;
    }

    private static bool TryCloseRetrieveWindow()
    {
        if (!CanIssueUiAction() || !TryGetMaterializeAddon(out var addon))
        {
            return false;
        }

        AddonCallback.Fire(addon, true, -1);
        lastUiActionAtUtc = DateTime.UtcNow;
        StatusMessage = $"Closing Retrieve Materia for {TargetName}...";
        LoggingUtil.Debug(StatusMessage);
        return true;
    }

    private static bool TryGetLiveTargetItem(out GameInventoryItem item)
    {
        var inventoryItems = Svc.GameInventory.GetInventoryItems(targetContainerType);

        if (targetSlot < 0 || targetSlot >= inventoryItems.Length)
        {
            item = default;
            return false;
        }

        item = inventoryItems[targetSlot];
        return !item.IsEmpty && item.BaseItemId == targetBaseItemId;
    }

    private static bool TryGetInventoryItemPointer(GameInventoryItem item, out InventoryItem* inventoryItem)
    {
        inventoryItem = (InventoryItem*)item.Address;
        return inventoryItem != null;
    }

    private static bool TryGetMaterializeAddon(out AtkUnitBase* addon)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("Materialize", out addon) && GenericHelpers.IsAddonReady(addon))
        {
            return true;
        }

        addon = null;
        return false;
    }

    private static AgentSalvage* GetSalvageAgent()
    {
        var uiModule = (UIModule*)Svc.GameGui.GetUIModule().Address;
        return uiModule == null ? null : (AgentSalvage*)uiModule->GetAgentModule()->GetAgentByInternalId(AgentId.Salvage);
    }

    private static string ResolveItemName(GameInventoryItem item)
    {
        var sheet = Svc.Data.GetExcelSheet<Item>();
        return sheet?.GetRow(item.BaseItemId).Name.ToString() ?? $"Item {item.BaseItemId}";
    }

    private static bool HasTimedOut(TimeSpan timeout)
    {
        return DateTime.UtcNow - stateStartedAtUtc > timeout;
    }

    private static bool CanIssueUiAction()
    {
        return DateTime.UtcNow - lastUiActionAtUtc >= UiActionThrottle;
    }

    private static void SetState(DMSState state, string status)
    {
        State = state;
        StatusMessage = status;
        stateStartedAtUtc = DateTime.UtcNow;
        LoggingUtil.Debug(status);
    }

    private static void Complete(string message)
    {
        ReportChat(message);
        DisablePlugin(message);
    }

    private static void Fail(string message)
    {
        LoggingUtil.Warning(message);
        ReportChat(message);
        DisablePlugin(message);
    }

    private static void ReportChat(string message)
    {
        if (P.Config.PrintChatStatus)
        {
            Svc.Chat.Print($"[DEMATSYNTH] {message}");
        }

        LoggingUtil.Info(message);
    }

    private static string BuildOperationDescription()
    {
        return (shouldRetrieve, shouldDesynth) switch
        {
            (true, true) => "retrieve + desynthesis",
            (true, false) => "materia retrieval",
            (false, true) => "desynthesis",
            _ => "nothing",
        };
    }
}
