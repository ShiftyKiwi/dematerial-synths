using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using DEMATSYNTH.Scheduler;
using Item = Lumina.Excel.Sheets.Item;

namespace DEMATSYNTH.ContextMenus;

internal static unsafe class ContextSubMenuOptions
{
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
        SchedulerMain.Start(targetItem, itemName, shouldRetrieve, shouldDesynth);
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
