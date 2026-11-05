using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using PeepingCat.Helpers;

using System;
using System.Numerics;

namespace PeepingCat.Windows {
    public class ConfigWindow : Window {
        private Configuration Configuration;
        private FileDialogManager FileDialogManager;
        private SoundManager SoundManager;

        public ConfigWindow(Plugin plugin) : base("Peeping Cat Settings") {
            Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            Size = new Vector2(450, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
            FileDialogManager = new FileDialogManager();
            SoundManager = new SoundManager(plugin);

            Configuration = plugin.Configuration;
        }

        public override void Draw() {
            FileDialogManager.Draw();

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));

            if (ImGui.BeginTabBar("##ConfigTabs")) {
                if (ImGui.BeginTabItem("Pop-Up")) {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "Pop-Up Settings");
                    ImGui.Separator();
                    ImGui.Spacing();
                    
                    var tellOption = Configuration.TellOption;
                    if (ImGui.Checkbox("Send Tell", ref tellOption)) {
                        Configuration.TellOption = tellOption;
                        Configuration.Save();
                    }

                    var doteOption = Configuration.DoteOption;
                    if (ImGui.Checkbox("Use Emote", ref doteOption)) {
                        Configuration.DoteOption = doteOption;
                        Configuration.Save();
                    }

                    var buffOption = Configuration.BuffInfoOption;
                    if (ImGui.Checkbox("View Buffs", ref buffOption)) {
                        Configuration.BuffInfoOption = buffOption;
                        Configuration.Save();
                    }

                    var stalkOption = Configuration.StalkOption;
                    if (ImGui.Checkbox("Stalk", ref stalkOption)) {
                        Configuration.StalkOption = stalkOption;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Opens a window showing who a player is targeting and who is or has been targeting them.");
                    }

                    ImGui.Spacing();
                    ImGui.Spacing();

                    var searchInfoOption = Configuration.SearchInfoOption;
                    if (ImGui.Checkbox("Open Context Menu", ref searchInfoOption)) {
                        Configuration.SearchInfoOption = searchInfoOption;
                        Configuration.Save();
                    }

                    var viewSearchInfoOption = Configuration.ViewSearchInfoOption;
                    if (ImGui.Checkbox("View Search Info", ref viewSearchInfoOption)) {
                        Configuration.ViewSearchInfoOption = viewSearchInfoOption;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Looks the player up through the game's player search and opens their Search Info window. Only works for players who are online and searchable.");
                    }

                    var apOption = Configuration.AdventurePlateOption;
                    if (ImGui.Checkbox("View Adventurer Plate", ref apOption)) {
                        Configuration.AdventurePlateOption = apOption;
                        Configuration.Save();
                    }

                    var exOption = Configuration.ExamineOption;
                    if (ImGui.Checkbox("Examine", ref exOption)) {
                        Configuration.ExamineOption = exOption;
                        Configuration.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Window")) {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "Main Window");
                    ImGui.Separator();
                    ImGui.Spacing();

                    var openOnLogin = Configuration.OpenOnLogin;
                    if (ImGui.Checkbox("Open on Login", ref openOnLogin)) {
                        Configuration.OpenOnLogin = openOnLogin;
                        Configuration.Save();
                    }

                    var showSettingsButton = Configuration.ShowSettingsButton;
                    if (ImGui.Checkbox("Show Settings Button", ref showSettingsButton)) {
                        Configuration.ShowSettingsButton = showSettingsButton;
                        Configuration.Save();
                    }

                    var allowMoving = Configuration.AllowMovingMainWindow;
                    if (ImGui.Checkbox("Allow Moving the Main Window", ref allowMoving)) {
                        Configuration.AllowMovingMainWindow = allowMoving;
                        Configuration.Save();
                    }

                    var allowResizing = Configuration.AllowResizingMainWindow;
                    if (ImGui.Checkbox("Allow Resizing the Main Window", ref allowResizing)) {
                        Configuration.AllowResizingMainWindow = allowResizing;
                        Configuration.Save();
                    }

                    var allowEscape = Configuration.AllowClosingWithEscape;
                    if (ImGui.Checkbox("Allow closing the Main Window with the Escape Key", ref allowEscape)) {
                        Configuration.AllowClosingWithEscape = allowEscape;
                        Configuration.Save();
                    }

                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "Visibility");
                    ImGui.Separator();
                    ImGui.Spacing();

                    var showInCombat = Configuration.ShowInCombat;
                    if (ImGui.Checkbox("Show Window while in Combat", ref showInCombat)) {
                        Configuration.ShowInCombat = showInCombat;
                        Configuration.Save();
                    }

                    var showInInstance = !Configuration.HideWindowWhileInDuty;
                    if (ImGui.Checkbox("Show Window while in Instance", ref showInInstance)) {
                        Configuration.HideWindowWhileInDuty = !showInInstance;
                        Configuration.Save();
                    }

                    var showInCutscenes = Configuration.ShowInCutscenes;
                    if (ImGui.Checkbox("Show Window while in Cutscenes", ref showInCutscenes)) {
                        Configuration.ShowInCutscenes = showInCutscenes;
                        Configuration.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Sound")) {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.6f, 0.8f, 1.0f, 1.0f), "Sound Settings");
                    ImGui.Separator();
                    ImGui.Spacing();

                    var soundEnabled = Configuration.SoundEnabled;
                    if (ImGui.Checkbox("Enable Sound", ref soundEnabled)) {
                        Configuration.SoundEnabled = soundEnabled;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Enable or disable the plugin’s sound playback.");
                    }

                    if (Configuration.SoundEnabled) {
                        ImGui.Indent();
                        ImGui.Spacing();

                        float volume = Configuration.SoundVolume;
                        if (ImGui.SliderFloat("Volume", ref volume, 0.0f, 1.0f, "Volume: %.2f")) {
                            Configuration.SoundVolume = volume;
                            Configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Adjust the volume for sound effects.");
                        }

                        if (ImGui.Button("Test Sound")) {
                            SoundManager.PlaySound(true);
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Click to play a test sound with the current settings.");
                        }

                        ImGui.Spacing();
                        var soundwithoutwindow = Configuration.SoundEnabledWindowClosed;
                        if (ImGui.Checkbox("Play Sound when Window is Closed", ref soundwithoutwindow)) {
                            Configuration.SoundEnabledWindowClosed = soundwithoutwindow;
                            Configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("If enabled, sound will still play even if the Main Window is not visible.");
                        }

                        var suppressInDuty = Configuration.SuppressSoundWhileInDuty;
                        if (ImGui.Checkbox("Mute Alerts while in Duties", ref suppressInDuty)) {
                            Configuration.SuppressSoundWhileInDuty = suppressInDuty;
                            Configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Disables alert sounds whenever you are bound by a duty (e.g., dungeons, trials, PvP).");
                        }

                        var soundCooldown = Configuration.SoundCooldownSeconds;
                        ImGui.SetNextItemWidth(120);
                        if (ImGui.InputInt("Sound Delay (seconds)", ref soundCooldown)) {
                            Configuration.SoundCooldownSeconds = Math.Clamp(soundCooldown, 0, 3600);
                            Configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Minimum time before the same viewer can trigger another sound alert.");
                        }
                        
                        ImGui.Spacing();
                        ImGui.TextUnformatted("Path to Audio File:");
                        Vector2 buttonSize;
                        ImGui.PushFont(UiBuilder.IconFont);
                        try {
                            buttonSize = ImGuiHelpers.GetButtonSize(FontAwesomeIcon.Folder.ToIconString());
                        } finally {
                            ImGui.PopFont();
                        }
                        
                        var path = Configuration.SoundFilePath ?? "";
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X - 10);
                        if (ImGui.InputText("###sound-path", ref path, 1_000)) {
                            path = path.Trim();
                            bool isPath = path.Length == 0;
                            if (!isPath) {
                                string newpath = SoundManager.CopySoundFileToPluginDirectory(path);
                                if (newpath != "") {
                                    Configuration.SoundFilePath = newpath;
                                }
                            } else {
                                Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                            }
                            Configuration.Save();
                        }
                        
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Specify the path to your preferred audio file.");
                        }

                        ImGui.SameLine();

                        ImGui.PushFont(UiBuilder.IconFont);
                        try {
                            if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString())) {
                                FileDialogManager.OpenFileDialog(
                                    "Path to Audio File",
                                    ".wav,.mp3,.aif,.aiff,.wma,.aac",
                                    (selected, selectedPath) => {
                                        if (!selected) return;
                                        path = selectedPath.Trim();
                                        bool isPath = path.Length == 0;

                                        if (!isPath) {
                                            string newpath = SoundManager.CopySoundFileToPluginDirectory(path);
                                            if (newpath != "") Configuration.SoundFilePath = newpath;
                                        } else {
                                            Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                                        }
                                        Configuration.Save();
                                    }
                                );
                            }
                        } finally {
                            ImGui.PopFont();
                        }

                        if (ImGui.Button("Reset to Default Soundfile")) {
                            Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                            Configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Restores the default sound file that shipped with the plugin.");
                        }

                        ImGui.Unindent();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Colours")) {
                    var colorPickerFlags = ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.NoInputs;

                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.7f, 1.0f, 0.7f, 1.0f), "Colour Customization");
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.Text("Adjust the Colours for different states:");
                    ImGui.Indent();
                    ImGui.Spacing();

                    var targetingColor = Configuration.targetingColour;
                    if (ImGui.ColorEdit4("Active", ref targetingColor, colorPickerFlags)) {
                        Configuration.targetingColour = targetingColor;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Colour used when the target is peeping.");
                    }

                    var loadedColor = Configuration.loadedColour;
                    if (ImGui.ColorEdit4("Recent", ref loadedColor, colorPickerFlags)) {
                        Configuration.loadedColour = loadedColor;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Colour used for recently loaded/peeping targets.");
                    }

                    var unloadedColor = Configuration.unloadedColour;
                    if (ImGui.ColorEdit4("Away", ref unloadedColor, colorPickerFlags)) {
                        Configuration.unloadedColour = unloadedColor;
                        Configuration.Save();
                    }
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Colour used for Peeper that are currently not loaded.");
                    }

                    ImGui.Unindent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("About")) {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.8f, 1.0f), "About Peeping Cat");
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.Text("Peeping Cat");
                    ImGui.Spacing();
                    ImGui.Text($"Version: {Configuration.DevVersion}");
                    ImGui.Text("Author: Originally kcuY (Peeping Tim), Edited by BoxuChan");
                    ImGui.Spacing();
                    ImGui.TextWrapped("Description: A slightly different version of Peeping Tom, offering additional functions.");

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.PopStyleVar();
        }
    }
}
