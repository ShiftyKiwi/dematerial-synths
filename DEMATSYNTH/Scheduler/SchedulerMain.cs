using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using ECommons.UIHelpers.AddonMasterImplementations;
using static DEMATSYNTH.Enums.DMSState;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;
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
    private static int remainingMateriaCount;
    private static bool hasObservedRetrieveProgress;
    private static int lastRequestedMateriaCount;
    private static DateTime stateStartedAtUtc;
    private static DateTime lastUiActionAtUtc;
    private static DateTime lastRetrieveProgressAtUtc;

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
        remainingMateriaCount = GetRetrievableMateriaCount(item);
        hasObservedRetrieveProgress = false;
        lastRequestedMateriaCount = 0;
        TargetName = string.IsNullOrWhiteSpace(targetName) ? ResolveItemName(item) : targetName;
        lastUiActionAtUtc = DateTime.MinValue;
        lastRetrieveProgressAtUtc = DateTime.UtcNow;

        LastResult = $"Working on {TargetName}.";
        ReportChat($"Starting {BuildOperationDescription()} for {TargetName}.");

        if (shouldRetrieve)
        {
            return TryStartMateriaRetrieval(item);
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
        remainingMateriaCount = 0;
        hasObservedRetrieveProgress = false;
        lastRequestedMateriaCount = 0;
        lastUiActionAtUtc = DateTime.MinValue;
        lastRetrieveProgressAtUtc = DateTime.MinValue;
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
        return GetRetrievableMateriaCount(item) > 0;
    }

    private static void TickWaitingForRetrieveCompletion()
    {
        if (TryHandleRetrieveDialog())
        {
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

        var currentMateriaCount = GetRetrievableMateriaCount(item);
        if (currentMateriaCount > remainingMateriaCount)
        {
            remainingMateriaCount = currentMateriaCount;
        }

        if (currentMateriaCount > 0)
        {
            if (currentMateriaCount < remainingMateriaCount)
            {
                remainingMateriaCount = currentMateriaCount;
                hasObservedRetrieveProgress = true;
                lastRequestedMateriaCount = 0;
                lastRetrieveProgressAtUtc = DateTime.UtcNow;
                stateStartedAtUtc = DateTime.UtcNow;
                StatusMessage = $"Retrieved materia from {TargetName}; {currentMateriaCount} remaining.";
                LoggingUtil.Debug(StatusMessage);
                return;
            }

            if (hasObservedRetrieveProgress
                && DateTime.UtcNow - lastRetrieveProgressAtUtc > TimeSpan.FromMilliseconds(750)
                && currentMateriaCount != lastRequestedMateriaCount
                && TryStartMateriaRetrieval(item))
            {
                return;
            }

            if (HasTimedOut(ActionTimeout))
            {
                Fail(hasObservedRetrieveProgress
                    ? "Additional materia remain, but the next retrieve step did not start."
                    : "Materia retrieval did not finish.");
            }

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
        if (TryGetVisibleAddonMaster<SalvageDialog>("SalvageDialog", out var dialog))
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
        if (TryGetVisibleAddonMaster<SalvageDialog>("SalvageDialog", out var dialog))
        {
            if (P.Config.AutoConfirmDesynthesis)
            {
                dialog.Desynthesize();
            }

            return;
        }

        if (TryGetVisibleAddonMaster<ECommonsSalvageResult>("SalvageResult", out var result))
        {
            if (P.Config.AutoCloseDesynthesisResult)
            {
                result.Close();
            }

            Complete($"Desynthesized {TargetName}.");
            return;
        }

        if (TryGetVisibleAddonMaster<SalvageAutoDialog>("SalvageAutoDialog", out var autoDialog) && autoDialog.DesynthesisInactive)
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

    private static bool TryStartMateriaRetrieval(GameInventoryItem item)
    {
        if (!CanIssueUiAction())
        {
            return false;
        }

        if (!TryGetInventoryItemPointer(item, out var inventoryItem))
        {
            Fail("The target item no longer has a valid inventory address.");
            return false;
        }

        var eventFramework = EventFramework.Instance();
        if (eventFramework == null)
        {
            Fail("Could not access the materialize event framework.");
            return false;
        }

        eventFramework->MaterializeItem(inventoryItem, MaterializeEntryId.Retrieve);
        lastRequestedMateriaCount = Math.Max(GetRetrievableMateriaCount(item), 1);
        lastUiActionAtUtc = DateTime.UtcNow;
        SetState(WaitingForRetrieveCompletion, $"Retrieving materia from {TargetName}...");
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

    private static int GetRetrievableMateriaCount(GameInventoryItem item)
    {
        if (item.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        foreach (var materiaId in item.Materia)
        {
            if (materiaId != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryHandleRetrieveDialog()
    {
        if (TryGetVisibleAddonMaster<MateriaRetrieveDialog>("MateriaRetrieveDialog", out var retrieveDialog))
        {
            if (P.Config.AutoConfirmRetrieveMateria)
            {
                retrieveDialog.Begin();
                SetState(WaitingForRetrieveCompletion, $"Retrieving materia from {TargetName}...");
            }
            else
            {
                SetState(WaitingForRetrieveCompletion, $"Waiting for you to confirm Retrieve Materia for {TargetName}...");
            }

            return true;
        }

        if (TryGetVisibleAddonMaster<MaterializeDialog>("MaterializeDialog", out var materializeDialog))
        {
            if (P.Config.AutoConfirmRetrieveMateria)
            {
                materializeDialog.Materialize();
                SetState(WaitingForRetrieveCompletion, $"Retrieving materia from {TargetName}...");
            }
            else
            {
                SetState(WaitingForRetrieveCompletion, $"Waiting for you to confirm Retrieve Materia for {TargetName}...");
            }

            return true;
        }

        return false;
    }

    private static bool TryGetVisibleAddonMaster<T>(string addonName, out T addonMaster) where T : class, IAddonMasterBase
    {
        if (GenericHelpers.TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(addonName, out var addon) && addon->IsVisible)
        {
            addonMaster = (T)Activator.CreateInstance(typeof(T), (nint)addon)!;
            return true;
        }

        addonMaster = default!;
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
