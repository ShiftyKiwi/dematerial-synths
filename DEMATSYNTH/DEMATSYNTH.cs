using ECommons.Configuration;
using System.Collections.Generic;
using DEMATSYNTH.Config;
using DEMATSYNTH.Ui;
using DEMATSYNTH.ContextMenus;
using DEMATSYNTH.Scheduler;

namespace DEMATSYNTH;

public sealed partial class DEMATSYNTH : IDalamudPlugin
{
    public static string Name => "DEMATSYNTH";
    internal static DEMATSYNTH P = null!;
    internal readonly Configuration Config;

    // Yaml Config Loaders. For both loading a yaml in the config folder, and for embedded
    private static T LoadConfig<T>() where T : IYamlConfig, new()
    {
        var path = typeof(T).GetProperty("ConfigPath")!.GetValue(null)!.ToString()!;
        var config = YamlConfig.Load<T>(path);

        if (config == null)
        {
            PluginLog.Warning($"[{typeof(T).Name}] Config was null. Creating new default.");
            config = new T();
            YamlConfig.Save(config, path);
        }

        PluginLog.Information($"[{typeof(T).Name}] Loaded from {path}");
        return config;
    }

    private static T LoadEmbeddedConfig<T>(string resourceName) where T : IYamlConfig, new()
    {
        var config = YamlConfig.LoadFromResource<T>(resourceName);

        if (config == null)
        {
            PluginLog.Warning($"[{typeof(T).Name}] Embedded config was null. Returning new default.");
            config = new T();
        }

        PluginLog.Information($"[{typeof(T).Name}] Loaded from embedded resource: {resourceName}");
        return config;
    }

    // Window's that I use, base window to the settings... need these to actually show shit 
    internal WindowSystem windowSystem;
    internal MainWindow mainWindow;
    internal SettingsWindow settingsWindow;
    // internal OverlayWindow overlayWindow;
    // internal DebugWindow debugWindow;

    // // Internal IPC's that I use for... well plugins. 
    // internal LifestreamIPC Lifestream;
    // internal NavmeshIPC Navmesh;
    // internal PandoraIPC Pandora;
    // internal ArtisanIPC Artisan;
    // internal VislandIPC Visland;
    // internal AutoHookIPC AutoHook;
    // internal IceCosmicExplorationIPC IceIpc;

    public DEMATSYNTH(IDalamudPluginInterface pi)
    {
        P = this;
        ECommonsMain.Init(pi, P, Module.DalamudReflector, ECommons.Module.ObjectFunctions);
        // PictoService.Initialize(pi);

        Config = pi.GetPluginConfig() as Configuration ?? new();
        Config.Initialize(pi);
        Config.Save();

        // //IPC's that are used
        // Lifestream = new();
        // Navmesh = new();
        // Pandora = new();
        // Artisan = new();
        // Visland = new();
        // AutoHook = new();
        // IceIpc = new();

        // all the windows
        ContextSubMenuOptions.Init();
        windowSystem = new();
        mainWindow = new();
        settingsWindow = new();
        // overlayWindow = new();
        // debugWindow = new();

        EzCmd.Add("/dematsynth", OnCommand, """
            Open plugin interface
            """);
        Init();
        Svc.Framework.Update += Tick;
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += () =>
        {
            mainWindow.IsOpen = true;
        };
        Svc.PluginInterface.UiBuilder.OpenConfigUi += () =>
        {
            settingsWindow.IsOpen = true;
        };
        // DictionaryCreation();
    }

    private static void Init()
    {
        
    }

    private void Tick(object _)
    {
        if (Svc.Objects.LocalPlayer != null)
        {
            SchedulerMain.Tick();
        }
        else
        {
            SchedulerMain.DisablePlugin("Player unavailable.");
        }
        // GenericManager.Tick();
        // TextAdvancedManager.Tick();
        // YesAlreadyManager.Tick();
    }

    public void Dispose()
    {
        GenericHelpers.Safe(() => Svc.Framework.Update -= Tick);
        GenericHelpers.Safe(() => Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw);
        GenericHelpers.Safe(ContextSubMenuOptions.Dispose);
        GenericHelpers.Safe(mainWindow.Dispose);
        GenericHelpers.Safe(settingsWindow.Dispose);
        // GenericHelpers.Safe(TextAdvancedManager.UnlockTA);
        // GenericHelpers.Safe(YesAlreadyManager.Unlock);
        ECommonsMain.Dispose();
        // PictoService.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var subcommands = args.Split(' ');

        if (subcommands.Length == 0 || args == "")
        {
            mainWindow.IsOpen = !mainWindow.IsOpen;
            return;
        }

        var firstArg = subcommands[0];

        if (firstArg.ToLower() == "d" || firstArg.ToLower() == "debug")
        {
            // debugWindow.IsOpen = true;
            return;
        }
        else if (firstArg.ToLower() is "s" or "settings" or "config")
        {
            settingsWindow.IsOpen = !settingsWindow.IsOpen;
            return;
        }
        else if (firstArg.ToLower() == "stop")
        {
            SchedulerMain.DisablePlugin("Stopped from command.");
            return;
        }
        else if (firstArg.ToLower() == "status")
        {
            Svc.Chat.Print($"[DEMATSYNTH] {SchedulerMain.StatusMessage}");
            return;
        }
        // else if (firstArg.ToLower() == "clear")
        // {
        //     foreach (var mission in C.MissionConfig)
        //     {
        //         mission.Value.Enabled = false;
        //     }
        //     C.Save();
        // }
        // else if (firstArg.ToLower() == "stop")
        // {
        //     SchedulerMain.DisablePlugin();
        // }
        // else if (firstArg.ToLower() == "start")
        // {
        //     SchedulerMain.EnablePlugin();
        // }
        // else if (firstArg.ToLower() == "add")
        // {
        //     uint[] ids = [.. subcommands.Skip(1).Select(uint.Parse)];
        //     var idSet = new HashSet<uint>(ids);
        //     if (ids.Length == 0) return;

        //     foreach (var id in idSet)
        //     {
        //         if (C.MissionConfig.TryGetValue(id, out var mission))
        //         {
        //             mission.Enabled = true;
        //         }
        //     }
        //     C.Save();
        // }
        // else if (firstArg.ToLower() == "remove")
        // {
        //     uint[] ids = [.. subcommands.Skip(1).Select(uint.Parse)];
        //     var idSet = new HashSet<uint>(ids);
        //     if (ids.Length == 0) return;

        //     foreach (var id in idSet)
        //     {
        //         if (C.MissionConfig.TryGetValue(id, out var mission))
        //         {
        //             mission.Enabled = false;
        //         }
        //     }
        //     C.Save();
        // }
        // else if (firstArg.ToLower() == "toggle")
        // {
        //     uint[] ids = [.. subcommands.Skip(1).Select(uint.Parse)];
        //     var idSet = new HashSet<uint>(ids);
        //     if (ids.Length == 0) return;

        //     foreach (var id in idSet)
        //     {
        //         if (C.MissionConfig.TryGetValue(id, out var mission))
        //         {
        //             mission.Enabled = !mission.Enabled;
        //         }
        //     }
        //     C.Save();
        // }
        // else if (firstArg.ToLower() == "only")
        // {
        //     uint[] ids = [.. subcommands.Skip(1).Select(uint.Parse)];
        //     var idSet = new HashSet<uint>(ids);
        //     if (ids.Length == 0) return;

        //     foreach (var mission in C.MissionConfig.Where(x => x.Value.Enabled))
        //     {
        //         mission.Value.Enabled = false;
        //     }
        //     foreach (var id in idSet)
        //     {
        //         if (C.MissionConfig.TryGetValue (id, out var mission))
        //         {
        //             mission.Enabled = true;
        //         }
        //     }
        // }
        // else if (firstArg.ToLower() == "flag")
        // {
        //     if (subcommands.Length != 2) return;
        //     if (!PlayerHelper.IsInCosmicZone()) return;

        //     int missionId = int.Parse(subcommands[1]);
        //     var info = SheetMissionDict.FirstOrDefault(mission => mission.Key == missionId);
        //     if (info.Value == default) return;
        //     if (info.Value.MarkerId == 0) return;

        //     Utils.SetGatheringRing(info.Value.TerritoryId, (int)info.Value.MapPosition.X, (int)info.Value.MapPosition.Y, info.Value.Radius, info.Value.Name);
        // }
        else if (firstArg.ToLower() == "help")
        {
            string helpMessage = """
                [DEMATSYNTH] Commands:
                /dematsynth - Open the main window
                /dematsynth settings - Open settings
                /dematsynth status - Print the current status
                /dematsynth stop - Stop the active automation
                """;
            Svc.Chat.Print(helpMessage);
        }
    }
}
