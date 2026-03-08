using Dalamud.Interface.Utility.Raii;
using DEMATSYNTH.Scheduler;
using DEMATSYNTH.Ui.SettingTabs;

namespace DEMATSYNTH.Ui;

internal class SettingsWindow : Window
{
    private readonly string[] settingOptions = ["Retrieve Materia", "Desynthesis"];
    private string selectedSetting = "Retrieve Materia";

    public SettingsWindow() :
        base($"Retrieve Materia & Desynthesis Settings {P.GetType().Assembly.GetName().Version} ###MESettingsWindow")
    {
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(520, 320),
            MaximumSize = new Vector2(1600, 1200),
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
        if (ImGui.BeginChild("SettingsNav", new Vector2(180, 0), true))
        {
            foreach (var setting in settingOptions)
            {
                if (ImGui.Button(setting, new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    selectedSetting = setting;
                }
            }

#if DEBUG
            if (ImGui.Button("Debug", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                selectedSetting = "Debug";
            }
#endif

            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGui.BeginChild("SettingsContent", new Vector2(0, 0), true))
        {
            switch (selectedSetting)
            {
                case "Retrieve Materia":
                    DrawRetrieveSettings();
                    break;
                case "Desynthesis":
                    DrawDesynthesisSettings();
                    break;
#if DEBUG
                case "Debug":
                    DebugTab.Draw();
                    break;
#endif
            }

            ImGui.EndChild();
        }
    }

    private static void DrawRetrieveSettings()
    {
        ImGui.TextWrapped("Controls how the inventory context-menu action handles melded items before desynthesis.");
        ImGui.Separator();

        DrawCheckbox(
            "Enable inventory context-menu action",
            P.Config.EnableContextMenu,
            value => P.Config.EnableContextMenu = value);

        DrawCheckbox(
            "Retrieve attached materia before desynthing",
            P.Config.RetrieveMateriaBeforeDesynth,
            value => P.Config.RetrieveMateriaBeforeDesynth = value);

        using (ImRaii.Disabled(!P.Config.RetrieveMateriaBeforeDesynth))
        {
            DrawCheckbox(
                "Auto-confirm the Retrieve Materia dialog",
                P.Config.AutoConfirmRetrieveMateria,
                value => P.Config.AutoConfirmRetrieveMateria = value);
        }

        ImGui.Spacing();
        ImGui.TextWrapped("If this phase is enabled, `Dematerialize It` selects the game's native `Retrieve Materia` action for the clicked inventory item before moving on.");
    }

    private static void DrawDesynthesisSettings()
    {
        ImGui.TextWrapped("Controls the desynthesis half of the plugin and what happens after materia retrieval finishes.");
        ImGui.Separator();

        DrawCheckbox(
            "Run desynthesis after retrieval",
            P.Config.RunDesynthesis,
            value => P.Config.RunDesynthesis = value);

        using (ImRaii.Disabled(!P.Config.RunDesynthesis))
        {
            DrawCheckbox(
                "Auto-confirm the desynthesis dialog",
                P.Config.AutoConfirmDesynthesis,
                value => P.Config.AutoConfirmDesynthesis = value);

            DrawCheckbox(
                "Auto-close the desynthesis result popup",
                P.Config.AutoCloseDesynthesisResult,
                value => P.Config.AutoCloseDesynthesisResult = value);
        }

        DrawCheckbox(
            "Print status updates to chat",
            P.Config.PrintChatStatus,
            value => P.Config.PrintChatStatus = value);

        ImGui.Spacing();
        ImGui.TextWrapped($"Current scheduler status: {SchedulerMain.StatusMessage}");
    }

    private static void DrawCheckbox(string label, bool currentValue, Action<bool> setter)
    {
        var newValue = currentValue;
        if (!ImGui.Checkbox(label, ref newValue))
        {
            return;
        }

        setter(newValue);
        P.Config.Save();
    }
}
