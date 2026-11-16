using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using System;
using System.Numerics;

using PeepingCat.Helpers;

namespace PeepingCat.Windows {
    public class ConfigWindow : Window {
        private static readonly Vector4 Gold = new(1.0f, 0.84f, 0.0f, 1.0f);
        private static readonly Vector4 Blue = new(0.6f, 0.8f, 1.0f, 1.0f);
        private static readonly Vector4 Green = new(0.7f, 1.0f, 0.7f, 1.0f);
        private static readonly Vector4 Pink = new(1.0f, 0.8f, 0.8f, 1.0f);

        private const ImGuiColorEditFlags ColourFlags = ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.NoInputs;

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
                
                if (BeginScrollTab("IPC")) { 
                    DrawIpcTab();
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

        private static void DrawSubTitle(string text, Vector4 colour) {
            ImGui.Spacing();
            ImGui.TextColored(colour, text);
            ImGui.Spacing();
        }

        private static void Tooltip(string text) {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip(text);
            }
        }

        private static bool ColourRow(string label, ref Vector4 colour, string? tooltip = null, ImGuiColorEditFlags flags = ColourFlags) {
            bool changed = ImGui.ColorEdit4(label, ref colour, flags);
            if (tooltip != null) {
                Tooltip(tooltip);
            }
            return changed;
        }

        private static void DrawHint(string text) {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextDisabled(text);
            ImGui.PopTextWrapPos();
        }

        private void Toggle(string label, bool value, Action<bool> set, string? tooltip = null) {
            if (ImGui.Checkbox(label, ref value)) {
                set(value);
                Configuration.Save();
            }

            if (tooltip != null) {
                Tooltip(tooltip);
            }
        }

        private static float IconButtonWidth(FontAwesomeIcon icon) {
            ImGui.PushFont(UiBuilder.IconFont);
            try {
                return ImGuiHelpers.GetButtonSize(icon.ToIconString()).X;
            } finally {
                ImGui.PopFont();
            }
        }

        private void DrawPopUpTab() {
            DrawTitle("Pop-Up Settings", Gold);
            DrawHint("Choose which actions appear in the right-click menu for a player.");

            DrawSubTitle("Actions", Blue);
            ImGui.Indent();
            Toggle("Send Tell", Configuration.TellOption, v => Configuration.TellOption = v);
            Toggle("Use Emote", Configuration.EmoteOption, v => Configuration.EmoteOption = v);
            Toggle("Stalk", Configuration.StalkOption, v => Configuration.StalkOption = v, "Opens a window showing who a player is targeting and who is or has been targeting them.");
            
            var stalkOwnMinionPet = Configuration.StalkOwnMinionPet;
            if (ImGui.Checkbox("Stalk Own Minion/Pet Buttons", ref stalkOwnMinionPet)) {
                Configuration.StalkOwnMinionPet = stalkOwnMinionPet;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Adds buttons to the Main Window to Stalk your own Minion and Summons, while you have them out.");
            }
            
            Toggle("View Buffs", Configuration.BuffInfoOption, v => Configuration.BuffInfoOption = v);
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSubTitle("Player Info", Blue);
            ImGui.Indent();
            Toggle("Open Context Menu", Configuration.SearchInfoOption, v => Configuration.SearchInfoOption = v);
            Toggle("View Search Info", Configuration.ViewSearchInfoOption, v => Configuration.ViewSearchInfoOption = v, "Looks the player up through the game's player search and opens their Search Info window. Only works for players who are online and searchable.");
            Toggle("View Adventurer Plate", Configuration.AdventurePlateOption, v => Configuration.AdventurePlateOption = v);
            Toggle("Examine", Configuration.ExamineOption, v => Configuration.ExamineOption = v);
            ImGui.Unindent();
        }

        private void DrawIpcTab() {
            DrawTitle("IPC Integrations", Gold);
            DrawHint("These options won't do anything unless the matching plugins are installed and enabled.");
            ImGui.Spacing();

            var chat2Menu = Configuration.Chat2MenuOption;
            if (ImGui.Checkbox("Chat 2 Integration", ref chat2Menu)) {
                Configuration.Chat2MenuOption = chat2Menu;
                Configuration.Save();
                plugin.SetChat2Integration(chat2Menu);
            }
            Tooltip("Adds Peeping Cat's actions to Chat 2's right-click menu.");

            var useXim = Configuration.UseXIVMessenger;
            if (ImGui.Checkbox("XIV Instant Messenger Integration", ref useXim)) {
                Configuration.UseXIVMessenger = useXim;
                Configuration.Save();
            }
            Tooltip("Send Tell opens the XIV Instant Messenger chat with that player instead of the quick message window.");
            if (!plugin.HasXIVMessagerIpc()) {
                ImGui.SameLine();
                ImGui.TextDisabled("(not detected)");
            }

            var usePetNicknames = Configuration.UsePetNicknames;
            if (ImGui.Checkbox("Pet Nicknames Integration", ref usePetNicknames)) {
                Configuration.UsePetNicknames = usePetNicknames;
                Configuration.Save();
            }
            Tooltip("When a stalked player targets a minion or summon, shows the nickname set in Pet Nicknames instead of its default name.");
            if (!plugin.HasPetNicknames()) {
                ImGui.SameLine();
                ImGui.TextDisabled("(not detected)");
            }

        }

        private void DrawWindowTab() {
            DrawTitle("Window Settings", Gold);

            DrawSubTitle("Behaviour", Blue);
            ImGui.Indent();
            Toggle("Open on Login", Configuration.OpenOnLogin, v => Configuration.OpenOnLogin = v);
            Toggle("Allow closing the Main Window with the Escape Key", Configuration.AllowClosingWithEscape, v => Configuration.AllowClosingWithEscape = v);
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSubTitle("Main Window", Blue);
            ImGui.Indent();
            Toggle("Show Settings Button", Configuration.ShowSettingsButton, v => Configuration.ShowSettingsButton = v);
            Toggle("Allow Moving the Main Window", Configuration.AllowMovingMainWindow, v => Configuration.AllowMovingMainWindow = v);
            Toggle("Allow Resizing the Main Window", Configuration.AllowResizingMainWindow, v => Configuration.AllowResizingMainWindow = v);
            Toggle("Show \"No viewers yet.\" Text", Configuration.ShowNoViewersText, v => Configuration.ShowNoViewersText = v, "Shows a placeholder text in the Main Window and Stalker windows when nobody is viewing.");
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSubTitle("Visibility", Blue);
            ImGui.Indent();
            Toggle("Show Window while in Combat", Configuration.ShowInCombat, v => Configuration.ShowInCombat = v);
            Toggle("Show Window while in Instance", !Configuration.HideWindowWhileInDuty, v => Configuration.HideWindowWhileInDuty = !v);
            Toggle("Show Window while in Cutscenes", Configuration.ShowInCutscenes, v => Configuration.ShowInCutscenes = v);
            ImGui.Unindent();
        }

        private void DrawMarkersTab() {
            DrawTitle("Marker Settings", Gold);

            DrawSubTitle("Your Target", Blue);
            var markTargeted = Configuration.MarkTargeted;
            var targetedColour = Configuration.MarkTargetedColour;
            var targetedSize = Configuration.MarkTargetedSize;
            if (DrawMarkerBlock("targeted", "Mark your target", "Draws a marker on the player you are currently targeting.", ref markTargeted, ref targetedColour, ref targetedSize)) {
                Configuration.MarkTargeted = markTargeted;
                Configuration.MarkTargetedColour = targetedColour;
                Configuration.MarkTargetedSize = Math.Max(0f, targetedSize);
                Configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSubTitle("Targeting You", Blue);
            var markTargeting = Configuration.MarkTargeting;
            var targetingColour = Configuration.MarkTargetingColour;
            var targetingSize = Configuration.MarkTargetingSize;
            if (DrawMarkerBlock("targeting", "Mark players targeting you", "Draws a marker on players who are currently targeting you.", ref markTargeting, ref targetingColour, ref targetingSize)) {
                Configuration.MarkTargeting = markTargeting;
                Configuration.MarkTargetingColour = targetingColour;
                Configuration.MarkTargetingSize = Math.Max(0f, targetingSize);
                Configuration.Save();
            }
        }

        private static bool DrawMarkerBlock(string id, string label, string tooltip, ref bool enabled, ref Vector4 colour, ref float size) {
            bool changed = false;
            ImGui.PushID(id);
            changed |= ImGui.Checkbox(label, ref enabled);
            Tooltip(tooltip);

            ImGui.Indent();
            ImGui.BeginDisabled(!enabled);

            changed |= ColourRow("Colour", ref colour, null, ImGuiColorEditFlags.None);
            changed |= ImGui.SliderFloat("Size", ref size, 0f, 15f, "%.2f");

            ImGui.EndDisabled();
            ImGui.Unindent();

            ImGui.PopID();
            return changed;
        }

        private void DrawFiltersTab() {
            DrawTitle("Filter Settings", Gold);
            DrawHint("Choose who gets logged when they target you.");

            DrawSubTitle("Who to Log", Blue);
            ImGui.Indent();
            Toggle("Log party members", Configuration.LogParty, v => Configuration.LogParty = v);
            Toggle("Log alliance members", Configuration.LogAlliance, v => Configuration.LogAlliance = v);
            Toggle("Log targeters while in combat", Configuration.LogInCombat, v => Configuration.LogInCombat = v);
            Toggle("Log yourself", Configuration.DetectSelfTarget, v => Configuration.DetectSelfTarget = v);
            ImGui.Unindent();
        }

        private void DrawHistoryTab() {
            DrawTitle("History Settings", Gold);

            DrawSubTitle("Previous Targeters", Blue);
            ImGui.Indent();
            Toggle("Show previous targeters", Configuration.KeepHistory, v => Configuration.KeepHistory = v);

            ImGui.BeginDisabled(!Configuration.KeepHistory);
            Toggle("Record history when window is closed", Configuration.HistoryWhenClosed, v => Configuration.HistoryWhenClosed = v);

            bool keepOwn = Configuration.KeepOwnCompanionHistory;
            if (ImGui.Checkbox("Keep history for my own minion/pet", ref keepOwn)) {
                Configuration.KeepOwnCompanionHistory = keepOwn;
            }
            
            var numHistory = Math.Clamp(Configuration.NumHistory, 1, 50);
            if (ImGui.SliderInt("Previous targeters to keep", ref numHistory, 1, 50)) {
                Configuration.NumHistory = Math.Clamp(numHistory, 1, 50);
                Configuration.Save();
            }
            ImGui.EndDisabled();
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSubTitle("Timestamps", Blue);
            ImGui.Indent();
            Toggle("Show timestamps", Configuration.ShowTimestamps, v => Configuration.ShowTimestamps = v);

            ImGui.BeginDisabled(!Configuration.ShowTimestamps);
            Toggle("Use 24-hour time", Configuration.Use24HourTime, v => Configuration.Use24HourTime = v);
            ImGui.EndDisabled();
            ImGui.Unindent();
        }

        private void DrawSoundTab() {
            DrawTitle("Sound Settings", Blue);

            var soundEnabled = Configuration.SoundEnabled;
            if (ImGui.Checkbox("Enable Sound", ref soundEnabled)) {
                Configuration.SoundEnabled = soundEnabled;
                Configuration.Save();
            }
            Tooltip("Enable or disable the plugin's sound playback.");

            if (!Configuration.SoundEnabled) {
                ImGui.Spacing();
                ImGui.TextDisabled("Turn sound on to configure alerts.");
                return;
            }

            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var playWidth = IconButtonWidth(FontAwesomeIcon.Play);
            var browseWidth = IconButtonWidth(FontAwesomeIcon.FolderOpen);
            var resetWidth = IconButtonWidth(FontAwesomeIcon.Undo);
            var edge = ImGui.GetStyle().ScrollbarSize;

            DrawSubTitle("Volume", Blue);
            ImGui.Indent();

            var volumePercent = Configuration.SoundVolume * 100f;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - playWidth - spacing - edge);
            if (ImGui.SliderFloat("##volume", ref volumePercent, 0f, 100f, "%.0f%%")) {
                Configuration.SoundVolume = Math.Clamp(volumePercent / 100f, 0f, 1f);
                Configuration.Save();
            }
            Tooltip("Adjust the volume for sound effects.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("sound_test", FontAwesomeIcon.Play)) {
                SoundManager.PlaySound(true);
            }
            Tooltip("Play a test sound with the current settings.");

            ImGui.Unindent();

            DrawSubTitle("Alerts", Blue);
            ImGui.Indent();

            var soundwithoutwindow = Configuration.SoundEnabledWindowClosed;
            if (ImGui.Checkbox("Play Sound when Window is Closed", ref soundwithoutwindow)) {
                Configuration.SoundEnabledWindowClosed = soundwithoutwindow;
                Configuration.Save();
            }
            Tooltip("If enabled, sound will still play even if the Main Window is not visible.");
            Toggle("Mute Alerts while in Duties", Configuration.SuppressSoundWhileInDuty, v => Configuration.SuppressSoundWhileInDuty = v, "Disables alert sounds whenever you are bound by a duty (e.g., dungeons, trials, raids).");
            Toggle("Mute Alerts while in PvP", Configuration.SuppressSoundWhileInPvP, v => Configuration.SuppressSoundWhileInPvP = v, "Disables alert sounds while you are in PvP.");

            ImGui.Spacing();
            var soundCooldown = Configuration.SoundCooldownSeconds;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Sound Delay (seconds)", ref soundCooldown)) {
                Configuration.SoundCooldownSeconds = Math.Clamp(soundCooldown, 0, 3600);
                Configuration.Save();
            }
            Tooltip("Minimum time before the same viewer can trigger another sound alert.");

            ImGui.Unindent();

            DrawSubTitle("Audio File", Blue);
            ImGui.Indent();

            var path = Configuration.SoundFilePath ?? "";
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - browseWidth - resetWidth - spacing * 2 - edge);
            if (ImGui.InputText("##sound-path", ref path, 1_000)) {
                path = path.Trim();
                if (path.Length > 0) {
                    string newpath = SoundManager.CopySoundFileToPluginDirectory(path);
                    if (newpath != "") {
                        Configuration.SoundFilePath = newpath;
                    }
                } else {
                    Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                }
                Configuration.Save();
            }
            Tooltip("Specify the path to your preferred audio file.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("sound_browse", FontAwesomeIcon.FolderOpen)) {
                FileDialogManager.OpenFileDialog("Path to Audio File", ".wav,.mp3,.aif,.aiff,.wma,.aac", (selected, selectedPath) => {
                        if (!selected) {
                            return;
                        }
                        
                        var chosen = selectedPath.Trim();
                        if (chosen.Length > 0) {
                            string newpath = SoundManager.CopySoundFileToPluginDirectory(chosen);
                            if (newpath != "") Configuration.SoundFilePath = newpath;
                        } else {
                            Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                        }
                        
                        Configuration.Save(); 
                    }
                );
            }
            Tooltip("Browse for an audio file.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("sound_reset", FontAwesomeIcon.Undo)) {
                Configuration.SoundFilePath = Configuration.OriginalSoundFile;
                Configuration.Save();
            }
            Tooltip("Restore the default sound file that shipped with the plugin.");

            ImGui.Unindent();
        }

        private void DrawColoursTab() {
            var colorPickerFlags = ImGuiColorEditFlags.None;

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
            
            DrawTitle("Nearby Players", Green);
            ImGui.Indent();

            var colourMoodles = Configuration.ColourMoodlesNearby;
            if (ImGui.Checkbox("Colour players with Moodles", ref colourMoodles)) {
                Configuration.ColourMoodlesNearby = colourMoodles;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Colours the names of players in the Nearby Players window who currently have Moodles applied.");
            }

            var moodlesColor = Configuration.moodlesColour;
            if (ImGui.ColorEdit4("Moodles", ref moodlesColor, colorPickerFlags)) {
                Configuration.moodlesColour = moodlesColor;
                Configuration.Save();
            }

            if (!plugin.HasMoodlesIpc()) {
                ImGui.TextDisabled("Moodles not detected.");
            }

            ImGui.Unindent();
        }

        private static void AboutRow(string label, string value) {
            ImGui.TextDisabled(label);
            ImGui.SameLine(110);
            ImGui.TextUnformatted(value);
        }

        private void DrawAboutTab() {
            DrawTitle("About Peeping Cat", Pink);

            ImGui.Indent();
            AboutRow("Version", Configuration.DevVersion);
            AboutRow("Original", "thakyZ (Peeping Tom)");
            AboutRow("Fork", "kcuY (Peeping Tim)");
            AboutRow("Edited by", "BoxuChan (Peeping Cat)");
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.Spacing();

            DrawHint("A slightly different version of Peeping Tom, offering additional functions.");
        }
    }
}
