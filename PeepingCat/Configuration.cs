using Dalamud.Configuration;

using System;
using System.IO;
using System.Numerics;

namespace PeepingCat;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public bool SoundEnabled { get; set; }
    public bool SoundEnabledWindowClosed { get; set; }
    public bool SuppressSoundWhileInDuty { get; set; } = true;
    public bool HideWindowWhileInDuty { get; set; }
    public float SoundVolume { get; set; } = 0.5f;
    public int SoundCooldownSeconds { get; set; } = 5;
    public static string OriginalSoundFile => Path.Combine(BasePath, "assets", "alert.wav");
    public string SoundFilePath { get; set; } = OriginalSoundFile;
    public bool OpenOnLogin { get; set; } = true;
    public bool ShowSettingsButton { get; set; } = true;
    public bool AllowMovingMainWindow { get; set; } = true;
    public bool AllowResizingMainWindow { get; set; } = true;
    public bool AllowClosingWithEscape { get; set; }
    public bool ShowInCombat { get; set; }
    public bool ShowInCutscenes { get; set; }
    public bool TellOption { get; set; } = true;
    public bool DoteOption { get; set; } = true;
    public bool AdventurePlateOption { get; set; } = true;
    public bool ExamineOption { get; set; } = true;
    public bool StalkOption { get; set; }
    public bool SearchInfoOption { get; set; } = true;
    public bool ViewSearchInfoOption { get; set; } = true;
    public bool BuffInfoOption { get; set; } = true;
    public Vector4 targetingColour { get; set; } = new Vector4(0.0431f, 0.9569f, 0.1804f, 1.0000f);
    public Vector4 unloadedColour { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 1f);
    public Vector4 loadedColour { get; set; } = new Vector4(1f, 1f, 1f, 1f);

    public readonly string DevVersion = "2.2.0.1";
    
    public static string BasePath = Plugin.PluginInterface.GetPluginConfigDirectory();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
