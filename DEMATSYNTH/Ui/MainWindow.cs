using Dalamud.Interface.Utility.Raii;
using DEMATSYNTH.Scheduler;
using System.Reflection;

namespace DEMATSYNTH.Ui;

internal class MainWindow : Window
{
    public MainWindow() :
#if DEBUG
        base($"Dematerial Desynthesis {P.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion} ###DEMATSYNTHMainWindow")
#else
        base($"Dematerial Desynthesis {P.GetType().Assembly.GetName().Version} ###DEMATSYNTHMainWindow")
#endif
    {
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(420, 240),
            MaximumSize = new Vector2(1200, 900),
        };

        P.windowSystem.AddWindow(this);
        AllowPinning = true;
    }

    public void Dispose()
    {
        P.windowSystem.RemoveWindow(this);
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Right-click a melded or desynthable inventory item and choose `Dematerialize It` to run the configured automation.");
        ImGui.Spacing();

        if (ImGui.Button("Settings", new Vector2(140, 0)))
        {
            P.settingsWindow.IsOpen = !P.settingsWindow.IsOpen;
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(!SchedulerMain.IsBusy))
        {
            if (ImGui.Button("Stop Current Run", new Vector2(140, 0)))
            {
                SchedulerMain.DisablePlugin("Stopped from the main window.");
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Current Status");
        ImGui.TextWrapped(SchedulerMain.StatusMessage);
        ImGui.Spacing();

        ImGui.TextUnformatted("Target");
        ImGui.TextWrapped(SchedulerMain.TargetName);
        ImGui.Spacing();

        ImGui.TextUnformatted("Last Result");
        ImGui.TextWrapped(SchedulerMain.LastResult);
        ImGui.Spacing();

        ImGui.TextUnformatted("Configured Behavior");
        ImGui.TextWrapped(BuildBehaviorSummary());
    }

    private static string BuildBehaviorSummary()
    {
        var retrieve = P.Config.RetrieveMateriaBeforeDesynth;
        var desynth = P.Config.RunDesynthesis;

        return (retrieve, desynth) switch
        {
            (true, true) => "The context-menu action retrieves attached materia first, then opens and confirms desynthesis.",
            (true, false) => "The context-menu action only retrieves attached materia.",
            (false, true) => "The context-menu action skips materia retrieval and goes straight to desynthesis.",
            _ => "The context-menu action is effectively disabled. Re-enable at least one phase in Settings.",
        };
    }
}
