using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace DEMATSYNTH.Config;

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool EnableContextMenu { get; set; } = true;
    public bool RetrieveMateriaBeforeDesynth { get; set; } = true;
    public bool RunDesynthesis { get; set; } = true;
    public bool AutoConfirmRetrieveMateria { get; set; } = true;
    public bool AutoConfirmDesynthesis { get; set; } = true;
    public bool AutoCloseDesynthesisResult { get; set; } = true;
    public bool PrintChatStatus { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
