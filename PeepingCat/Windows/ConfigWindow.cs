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
        private static readonly Vector4 Gold = new(1.0f, 0.84f, 0.0f, 1.0f);
        private static readonly Vector4 Blue = new(0.6f, 0.8f, 1.0f, 1.0f);
        private static readonly Vector4 Green = new(0.7f, 1.0f, 0.7f, 1.0f);
        private static readonly Vector4 Pink = new(1.0f, 0.8f, 0.8f, 1.0f);

        private Plugin plugin;
        private Configuration Configuration;
        private FileDialogManager FileDialogManager;
        private SoundManager SoundManager;

        public ConfigWindow(Plugin plugin) : base("Peeping Cat - Settings") {
            Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(560, 300),
                MaximumSize = new Vector2(1000, 900)
            };

            Size = new Vector2(600, 420);
            SizeCondition = ImGuiCond.FirstUseEver;
            FileDialogManager = new FileDialogManager();
            SoundManager = new SoundManager(plugin);

            this.plugin = plugin;
            Configuration = plugin.Configuration;
        }

        public override void Draw() {
            FileDialogManager.Draw();

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));

            if (ImGui.BeginTabBar("##ConfigTabs", ImGuiTabBarFlags.FittingPolicyResizeDown)) {
                if (BeginScrollTab("Pop-Up")) {
                    DrawPopUpTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("Window")) {
                    DrawWindowTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("Markers")) {
                    DrawMarkersTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("Filters")) {
                    DrawFiltersTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("History")) {
                    DrawHistoryTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("Sound")) {
                    DrawSoundTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("Colours")) {
                    DrawColoursTab();
                    EndScrollTab();
                }

                if (BeginScrollTab("About")) {
                    DrawAboutTab();
                    EndScrollTab();
                }

                ImGui.EndTabBar();
            }

            ImGui.PopStyleVar();
        }

        private static bool BeginScrollTab(string label) {
            if (!ImGui.BeginTabItem(label)) {
                return false;
            }

            ImGui.BeginChild($"##scroll_{label}", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.5f);
            return true;
        }

        private static void EndScrollTab() {
            ImGui.PopItemWidth();
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        private static void DrawTitle(string text, Vector4 colour) {
            ImGui.Spacing();
            ImGui.TextColored(colour, text);
            ImGui.Separator();
            ImGui.Spacing();
        }

        private void DrawPopUpTab() {
            DrawTitle("Pop-Up Settings", Gold);

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

            ImGui.Spacing();
            ImGui.Spacing();

            var chat2Menu = Configuration.Chat2MenuOption;
            if (ImGui.Checkbox("Chat 2 Context Menu Integration", ref chat2Menu)) {
                Configuration.Chat2MenuOption = chat2Menu;
                Configuration.Save();
                plugin.SetChat2Integration(chat2Menu);
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Adds Peeping Cat's actions to Chat 2's right-click menu. Does nothing if Chat 2 isn't installed.");
            }
        }

        private void DrawWindowTab() {
            DrawTitle("Window Settings", Gold);

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

            var showNoViewers = Configuration.ShowNoViewersText;
            if (ImGui.Checkbox("Show \"No viewers yet.\" Text", ref showNoViewers)) {
                Configuration.ShowNoViewersText = showNoViewers;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Shows a placeholder text in the Main Window and Stalker windows when nobody is viewing.");
            }

            DrawTitle("Visibility", Gold);

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
        }

        private void DrawMarkersTab() {
            DrawTitle("Marker Settings", Gold);

            var markTargeted = Configuration.MarkTargeted;
            if (ImGui.Checkbox("Mark your target", ref markTargeted)) {
                Configuration.MarkTargeted = markTargeted;
                Configuration.Save();
            }

            var targetedColour = Configuration.MarkTargetedColour;
            if (ImGui.ColorEdit4("Target mark colour", ref targetedColour)) {
                Configuration.MarkTargetedColour = targetedColour;
                Configuration.Save();
            }

            var targetedSize = Configuration.MarkTargetedSize;
            if (ImGui.DragFloat("Target mark size", ref targetedSize, 0.01f, 0f, 15f)) {
                Configuration.MarkTargetedSize = Math.Max(0f, targetedSize);
                Configuration.Save();
            }

            ImGui.Spacing();

            var markTargeting = Configuration.MarkTargeting;
            if (ImGui.Checkbox("Mark targeting you", ref markTargeting)) {
                Configuration.MarkTargeting = markTargeting;
                Configuration.Save();
            }

            var targetingColour = Configuration.MarkTargetingColour;
            if (ImGui.ColorEdit4("Targeting mark colour", ref targetingColour)) {
                Configuration.MarkTargetingColour = targetingColour;
                Configuration.Save();
            }

            var targetingSize = Configuration.MarkTargetingSize;
            if (ImGui.DragFloat("Targeting mark size", ref targetingSize, 0.01f, 0f, 15f)) {
                Configuration.MarkTargetingSize = Math.Max(0f, targetingSize);
                Configuration.Save();
            }
        }

        private void DrawFiltersTab() {
            DrawTitle("Filter Settings", Gold);

            var logParty = Configuration.LogParty;
            if (ImGui.Checkbox("Log party members", ref logParty)) {
                Configuration.LogParty = logParty;
                Configuration.Save();
            }

            var logAlliance = Configuration.LogAlliance;
            if (ImGui.Checkbox("Log alliance members", ref logAlliance)) {
                Configuration.LogAlliance = logAlliance;
                Configuration.Save();
            }

            var logInCombat = Configuration.LogInCombat;
            if (ImGui.Checkbox("Log targeters while in combat", ref logInCombat)) {
                Configuration.LogInCombat = logInCombat;
                Configuration.Save();
            }

            var logSelf = Configuration.DetectSelfTarget;
            if (ImGui.Checkbox("Log yourself", ref logSelf)) {
                Configuration.DetectSelfTarget = logSelf;
                Configuration.Save();
            }
        }

        private void DrawHistoryTab() {
            DrawTitle("History Settings", Gold);

            var keepHistory = Configuration.KeepHistory;
            if (ImGui.Checkbox("Show previous targeters", ref keepHistory)) {
                Configuration.KeepHistory = keepHistory;
                Configuration.Save();
            }

            var historyWhenClosed = Configuration.HistoryWhenClosed;
            if (ImGui.Checkbox("Record history when window is closed", ref historyWhenClosed)) {
                Configuration.HistoryWhenClosed = historyWhenClosed;
                Configuration.Save();
            }

            var numHistory = Configuration.NumHistory;
            if (ImGui.InputInt("Number of previous targeters to keep", ref numHistory)) {
                Configuration.NumHistory = Math.Max(0, Math.Min(50, numHistory));
                Configuration.Save();
            }

            var showTimestamps = Configuration.ShowTimestamps;
            if (ImGui.Checkbox("Show timestamps", ref showTimestamps)) {
                Configuration.ShowTimestamps = showTimestamps;
                Configuration.Save();
            }

            var use24Hour = Configuration.Use24HourTime;
            if (ImGui.Checkbox("Use 24-hour time", ref use24Hour)) {
                Configuration.Use24HourTime = use24Hour;
                Configuration.Save();
            }
        }

        private void DrawSoundTab() {
            DrawTitle("Sound Settings", Blue);

            var soundEnabled = Configuration.SoundEnabled;
            if (ImGui.Checkbox("Enable Sound", ref soundEnabled)) {
                Configuration.SoundEnabled = soundEnabled;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Enable or disable the plugin’s sound playback.");
            }

            if (!Configuration.SoundEnabled) {
                return;
            }

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
                            var chosen = selectedPath.Trim();
                            bool isPath = chosen.Length == 0;

                            if (!isPath) {
                                string newpath = SoundManager.CopySoundFileToPluginDirectory(chosen);
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

        private void DrawColoursTab() {
            var colorPickerFlags = ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.NoInputs;

            DrawTitle("Colour Settings", Green);

            ImGui.Text("Adjust the Colours for different states:");
            ImGui.Indent();
            ImGui.Spacing();

            var targetingColor = Configuration.targetingColour;
            if (ImGui.ColorEdit4("Active", ref targetingColor, colorPickerFlags)) {
                Configuration.targetingColour = targetingColor;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Colour used when a player is actively targeting you.");
            }

            var loadedColor = Configuration.loadedColour;
            if (ImGui.ColorEdit4("Recent", ref loadedColor, colorPickerFlags)) {
                Configuration.loadedColour = loadedColor;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Colour used for players recently targeting you (if they are in your current surroundings).");
            }

            var unloadedColor = Configuration.unloadedColour;
            if (ImGui.ColorEdit4("Away", ref unloadedColor, colorPickerFlags)) {
                Configuration.unloadedColour = unloadedColor;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Colour used for players recently targeting you (if they are not in your loaded area).");
            }

            ImGui.Unindent();
        }

        private void DrawAboutTab() {
            DrawTitle("About Peeping Cat", Pink);

            ImGui.Text("Peeping Cat");
            ImGui.Spacing();
            ImGui.Text($"Version: {Configuration.DevVersion}");
            ImGui.Text("Author: Originally thakyZ (Peeping Tom) & kcuY (Peeping Tim), Edited by BoxuChan");
            ImGui.Spacing();
            ImGui.TextWrapped("Description: A slightly different version of Peeping Tom, offering additional functions.");
        }
    }
}
