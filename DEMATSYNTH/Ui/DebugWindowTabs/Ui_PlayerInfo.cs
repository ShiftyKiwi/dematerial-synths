using ECommons.GameHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace DEMATSYNTH.Ui.DebugWindowTabs
{
    internal class Ui_PlayerInfo
    {
        public static void Draw()
        {
            ImGui.Text("Need to actually put the player info here. It got lost");
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Player Position: X:{Player.Position.X:N2}, Y:{Player.Position.Y:N2}, Z:{Player.Position.Z:N2}");
            ImGui.SameLine();
            if (ImGui.Button("Copy Vector2"))
            {
                ImGui.SetClipboardText($"{Player.Position.X:N2}f, {Player.Position.Z:N2}f");
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy Vector3"))
            {
                ImGui.SetClipboardText($"{Player.Position.X:N2}f, {Player.Position.Y:N2}f, {Player.Position.Z:N2}f");
            }
            ImGui.Text($"Job: {Player.Job}");
            ImGui.Text($"JobId: {Player.ClassJob.RowId}");
            ImGui.Text($"Current Territory/ZoneId: {Player.Territory}");
            if (Svc.Targets.Target != null)
            {
                var currentTarget = Svc.Targets.Target;
                if (ImGui.Button($"Name: {currentTarget.Name}"))
                {
                    ImGui.SetClipboardText(currentTarget.Name.ToString());
                }
                if (ImGui.Button($"Id: {currentTarget.BaseId}"))
                {
                    ImGui.SetClipboardText(currentTarget.BaseId.ToString());
                }
                if (ImGui.Button($"Position: X: {currentTarget.Position.X:N2}, Y: {currentTarget.Position.Y:N2}, Z: {currentTarget.Position.Z:N2}"))
                {
                    ImGui.SetClipboardText($"{currentTarget.Position.X:N2}f, {currentTarget.Position.Y:N2}f, {currentTarget.Position.Z:N2}f");
                }
                ImGui.Text($"Distance: {Player.DistanceTo(currentTarget):N2}");
            }

            // ImGui.Text($"Items on person: ");
            // foreach (var item in ConsumableInfo.Food)
            // {
            //     if (PlayerHelper.GetItemCount(item.Id, out var count) && count > 0)
            //         ImGui.Text($"{item.Name} | {item.Id}");
            // }
        }
    }
}
