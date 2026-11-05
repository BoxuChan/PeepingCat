using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using ECommons.DalamudServices;

using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PeepingCat.Windows {
    public static class EmoteWindowHost {
        private static readonly WindowSystem windowSystem = new("PeepingCatEmote");
        private static EmoteWindow? window;
        private static bool hooked;

        public static void Open(Plugin plugin, Plugin.ViewerInfo viewer) {
            if (!hooked) {
                Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
                hooked = true;
            }

            if (window == null) {
                window = new EmoteWindow(plugin);
                windowSystem.AddWindow(window);
            }

            window.Show(viewer);
        }

        public static void Dispose() {
            if (hooked) {
                Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
                hooked = false;
            }

            windowSystem.RemoveAllWindows();
            window = null;
        }
    }

    public class EmoteWindow : Window {
        private record EmoteEntry(uint Id, string Name, string Command);
        private const string WindowId = "###PeepingCatEmote";

        private readonly Plugin plugin;
        private Plugin.ViewerInfo? target;
        private List<EmoteEntry> emotes = new();
        private string search = string.Empty;
        private uint selectedId;
        private long nextPresenceCheck;
        private const long PresenceCheckIntervalMs = 250;

        public EmoteWindow(Plugin plugin) : base($"Emote{WindowId}", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse) {
            this.plugin = plugin;
            RespectCloseHotkey = true;
            ShowCloseButton = false;
        }

        public void Show(Plugin.ViewerInfo viewer) {
            target = viewer;
            WindowName = $"Emote - {viewer.Name}{WindowId}";
            search = string.Empty;
            selectedId = 0;
            LoadEmotes();
            nextPresenceCheck = 0;
            IsOpen = true;
        }

        public override void Update() {
            if (target == null) {
                return;
            }

            long now = Environment.TickCount64;
            if (now < nextPresenceCheck) {
                return;
            }
            
            nextPresenceCheck = now + PresenceCheckIntervalMs;
            if (!IsTargetPresent()) {
                IsOpen = false;
            }
        }

        private bool IsTargetPresent() {
            foreach (var obj in Svc.Objects) {
                if (obj is IPlayerCharacter pc && pc.Name.TextValue == target!.Name && string.Equals(plugin.GetWorldName(pc.HomeWorld.RowId), target.World, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        public override void Draw() {
            if (target == null) {
                return;
            }

            float width = 260f * ImGuiHelpers.GlobalScale;
            var selected = emotes.FirstOrDefault(e => e.Id == selectedId);

            ImGui.SetNextItemWidth(width);
            ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, float.MaxValue));
            if (ImGui.BeginCombo("##emoteCombo", selected?.Name ?? "Select an emote...")) {
                if (ImGui.IsWindowAppearing()) {
                    ImGui.SetKeyboardFocusHere();
                }
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##emoteSearch", "Search...", ref search, 64);
                ImGui.Separator();

                var filtered = string.IsNullOrWhiteSpace(search) ? emotes : emotes.Where(e => e.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

                ImGui.BeginChild("##emoteList", new Vector2(0, 220 * ImGuiHelpers.GlobalScale), false);
                if (filtered.Count == 0) {
                    ImGui.TextDisabled("No matching emotes");
                }
                
                foreach (var emote in filtered) {
                    bool isSelected = emote.Id == selectedId;
                    
                    if (ImGui.Selectable(emote.Name, isSelected)) {
                        selectedId = emote.Id;
                        ImGui.CloseCurrentPopup();
                    }
                    if (isSelected) {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndChild();
                ImGui.EndCombo();
            } else {
                search = string.Empty;
            }

            ImGui.Spacing();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            var buttonSize = new Vector2((width - spacing) / 2f, ImGui.GetFrameHeight() * 1.5f);
            var disabledGrey = new Vector4(0.20f, 0.20f, 0.20f, 1f);

            bool canSend = selected != null;
            if (!canSend) {
                ImGui.BeginDisabled();
            }
            
            ImGui.PushStyleColor(ImGuiCol.Button, canSend ? new Vector4(0.20f, 0.50f, 0.30f, 1f) : disabledGrey);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, canSend ? new Vector4(0.26f, 0.60f, 0.37f, 1f) : disabledGrey);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, canSend ? new Vector4(0.16f, 0.42f, 0.24f, 1f) : disabledGrey);
            bool send;
            
            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                send = ImGui.Button(FontAwesomeIcon.PaperPlane.ToIconString() + "##send", buttonSize);
            }
            
            ImGui.PopStyleColor(3);
            
            if (!canSend) {
                ImGui.EndDisabled();
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip("Send");
            }

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.50f, 0.22f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.60f, 0.28f, 0.28f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.42f, 0.18f, 0.18f, 1f));
            bool cancel;
            
            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                cancel = ImGui.Button(FontAwesomeIcon.Times.ToIconString() + "##cancel", buttonSize);
            }
            
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Cancel");
            }

            if (send && selected != null) {
                UseEmote(selected);
                IsOpen = false;
            }
            if (cancel) {
                IsOpen = false;
            }
        }

        private unsafe void LoadEmotes() {
            emotes.Clear();

            var sheet = Svc.Data.GetExcelSheet<Emote>();
            var ui = UIState.Instance();
            var seen = new HashSet<string>();

            foreach (var row in sheet) {
                string name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name) || !row.TextCommand.IsValid) {
                    continue;
                }

                string command = row.TextCommand.Value.Command.ToString();
                if (string.IsNullOrWhiteSpace(command) || !seen.Add(command)) {
                    continue;
                }

                if (row.UnlockLink != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(row.UnlockLink)) {
                    continue;
                }

                emotes.Add(new EmoteEntry(row.RowId, name, command));
            }

            emotes = emotes.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void UseEmote(EmoteEntry emote) {
            if (target == null) {
                return;
            }

            plugin.TargetCharacter(target);
            Svc.Framework.RunOnTick(() => SendChatCommand(emote.Command), TimeSpan.FromMilliseconds(100));
        }

        private static unsafe void SendChatCommand(string message) {
            var str = Utf8String.FromString(message);
            UIModule.Instance()->ProcessChatBoxEntry(str);
            str->Dtor(true);
        }
    }
}
