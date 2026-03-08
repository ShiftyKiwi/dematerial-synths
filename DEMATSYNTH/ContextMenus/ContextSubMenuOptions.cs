using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AtkReaderImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using DEMATSYNTH.Scheduler;
using Callback = ECommons.Automation.Callback;
using Item = Lumina.Excel.Sheets.Item;

namespace DEMATSYNTH.ContextMenus;

internal static unsafe class ContextSubMenuOptions
{
    private static readonly string[] RetrieveMateriaEntryNames = ["Retrieve Materia"];
    private static IContextMenu? contextMenu;

    public static void Init()
    {
        contextMenu = Svc.ContextMenu;
        contextMenu.OnMenuOpened += AddMenu;
        LoggingUtil.Debug("Initialized inventory context menus.");
    }

    private static void AddMenu(IMenuOpenedArgs args)
    {
        if (!P.Config.EnableContextMenu)
        {
            return;
        }

        if (args.MenuType != ContextMenuType.Inventory || args.Target is not MenuTargetInventory inventoryTarget)
        {
            return;
        }

        if (inventoryTarget.TargetItem is not { } targetItem || targetItem.IsEmpty)
        {
            return;
        }

        var shouldRetrieve = P.Config.RetrieveMateriaBeforeDesynth && SchedulerMain.CanRetrieveMateria(targetItem);
        var shouldDesynth = P.Config.RunDesynthesis;
        if (!shouldRetrieve && !shouldDesynth)
        {
            return;
        }

        var itemName = ResolveItemName(targetItem);
        var menuItem = new MenuItem
        {
            Name = "Dematerialize It",
            PrefixChar = 'D',
            PrefixColor = 706,
            IsEnabled = !SchedulerMain.IsBusy,
        };

        menuItem.OnClicked += _ => OnClicked(targetItem, itemName, shouldRetrieve, shouldDesynth);
        args.AddMenuItem(menuItem);
    }

    private static void OnClicked(GameInventoryItem targetItem, string itemName, bool shouldRetrieve, bool shouldDesynth)
    {
        if (!SchedulerMain.Start(targetItem, itemName, shouldRetrieve, shouldDesynth))
        {
            return;
        }

        if (shouldRetrieve && !TrySelectCurrentContextEntry(RetrieveMateriaEntryNames))
        {
            SchedulerMain.DisablePlugin("Could not find the game's Retrieve Materia entry for this item.");
            return;
        }

        if (shouldRetrieve)
        {
            SchedulerMain.NotifyRetrieveTriggered();
        }
    }

    private static bool TrySelectCurrentContextEntry(params string[] candidateTexts)
    {
        var addon = GetOpenContextMenu();
        if (addon == null || !GenericHelpers.IsAddonReady(addon))
        {
            return false;
        }

        var reader = new ReaderContextMenu(addon);
        for (var i = 0; i < reader.Entries.Count; i++)
        {
            var entryName = reader.Entries[i].Name;
            if (!MatchesEntry(entryName, candidateTexts))
            {
                continue;
            }

            Callback.Fire(addon, true, 0, i, 0);
            LoggingUtil.Debug($"Selected native context entry '{entryName}' at index {i}.");
            return true;
        }

        LoggingUtil.Warning($"Could not match any native context entry for: {string.Join(", ", candidateTexts)}");
        return false;
    }

    private static bool MatchesEntry(string entryName, string[] candidateTexts)
    {
        return candidateTexts.Any(candidate =>
            entryName.Equals(candidate, StringComparison.OrdinalIgnoreCase)
            || entryName.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static AtkUnitBase* GetOpenContextMenu()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var iconMenu) && iconMenu->IsVisible)
        {
            return iconMenu;
        }

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextMenu", out var contextMenuAddon) && contextMenuAddon->IsVisible)
        {
            return contextMenuAddon;
        }

        return null;
    }

    private static string ResolveItemName(GameInventoryItem item)
    {
        var sheet = Svc.Data.GetExcelSheet<Item>();
        return sheet?.GetRow(item.BaseItemId).Name.ToString() ?? $"Item {item.BaseItemId}";
    }

    public static void Dispose()
    {
        if (contextMenu != null)
        {
            contextMenu.OnMenuOpened -= AddMenu;
        }
    }
}
