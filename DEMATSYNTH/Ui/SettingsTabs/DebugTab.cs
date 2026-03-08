using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DEMATSYNTH.Scheduler;

namespace DEMATSYNTH.Ui.SettingTabs
{
    internal class DebugTab
    {
        public static void Draw()
        {
            ImGui.Checkbox("Force OOM Main", ref SchedulerMain.DebugOOMMain);
            ImGui.Checkbox("Force OOM Sub", ref SchedulerMain.DebugOOMSub);
            // ImGui.Checkbox("Legacy Failsafe WKSRecipe Select", ref C.FailsafeRecipeSelect);

            // var missionMap = new List<(string name, Func<byte> get, Action<byte> set)>
            //     {
            //         ("Sequence Missions", new Func<byte>(() => C.SequenceMissionPriority), new Action<byte>(v => { C.SequenceMissionPriority = v; C.Save(); })),
            //         ("Timed Missions", new Func<byte>(() => C.TimedMissionPriority), new Action<byte>(v => { C.TimedMissionPriority = v; C.Save(); })),
            //         ("Weather Missions", new Func<byte>(() => C.WeatherMissionPriority), new Action<byte>(v => { C.WeatherMissionPriority = v; C.Save(); }))
            //     };

            // var sorted = missionMap
            //     .Select((m, i) => new { Index = i, Name = m.name, Priority = m.get() })
            //     .OrderBy(m => m.Priority)
            //     .ToList();

            // using (ImRaii.Disabled(!PlayerHelper.IsInCosmicZone()))
            // {
            //     if (ImGui.Button("Refresh Forecast"))
            //     {
            //         WeatherForecastHandler.GetForecast();
            //     }
            // }
        }
    }
}
