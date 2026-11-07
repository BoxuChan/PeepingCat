using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using ECommons.DalamudServices;
using ECommons.GameFunctions;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;

namespace PeepingCat.Windows {
    public static class Popup {
        private static bool OpenBuffModal;
        private static string BuffOwner = string.Empty;
        private static List<MoodlesStatusInfo> Buffs = new();

        private static int LastBuffModalDrawFrame = -1;
        private const string BuffModalId = "###MoodlesBuffs";

        private static readonly Regex MoodlesTagRegex = new(@"\[/?(?:color|glow|i|b|u|s)(?:=[^\]]+)?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string CleanMoodlesText(string? text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            return MoodlesTagRegex.Replace(text, string.Empty).Replace("&", string.Empty).Trim();
        }

        private static readonly string[] MenuLabels = {
            "Send Tell", "Use Emote", "View Buffs (99)", "Stalk",
            "Open Context Menu", "View Search Info", "View Adventurer Plate", "Examine"
        };

        public static void OpenGameContextMenu(Plugin.ViewerInfo viewer) {
            IGameObject? character = Svc.Objects.SearchById(viewer.lastKnownGameObjectId);
            if (character == null) {
                return;
            }

            unsafe {
                Svc.Framework.RunOnTick(() => {
                    AgentHUD.Instance()->OpenContextMenuFromTarget(character.Struct());
                });
            }
        }
        
        public static void ExamineViewer(Plugin.ViewerInfo viewer) {
            IGameObject? character = Svc.Objects.SearchById(viewer.lastKnownGameObjectId);
            if (character == null) {
                return;
            }

            unsafe {
                Svc.Framework.RunOnTick(() => {
                    AgentInspect.Instance()->ExamineCharacter(character.EntityId);
                });
            }
        }
        
        public static bool IconMenuItem(FontAwesomeIcon icon, string label) {
            float scale = ImGuiHelpers.GlobalScale;
            ImGui.GetStyle();

            float leftPad = 6f * scale;
            float iconColumn = 20f * scale;
            float iconTextGap = 8f * scale;
            float rightPad = 10f * scale;
            float rowHeight = ImGui.GetFrameHeight() + 8f * scale;

            float maxText = 0f;
            foreach (var l in MenuLabels) {
                maxText = Math.Max(maxText, ImGui.CalcTextSize(l).X);
            }
            float rowWidth = leftPad + iconColumn + iconTextGap + maxText + rightPad;

            bool clicked = ImGui.Selectable($"##{label}", false, ImGuiSelectableFlags.None, new Vector2(rowWidth, rowHeight));

            var min = ImGui.GetItemRectMin();
            var drawList = ImGui.GetWindowDrawList();
            uint color = ImGui.GetColorU32(ImGuiCol.Text);

            string iconText = icon.ToIconString();
            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                var iconSize = ImGui.CalcTextSize(iconText);
                var iconPos = new Vector2(
                    min.X + leftPad + (iconColumn - iconSize.X) * 0.5f,
                    min.Y + (rowHeight - iconSize.Y) * 0.5f);
                drawList.AddText(iconPos, color, iconText);
            }

            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(min.X + leftPad + iconColumn + iconTextGap, min.Y + (rowHeight - textSize.Y) * 0.5f);
            drawList.AddText(textPos, color, label);

            return clicked;
        }

        public static void Draw(Plugin Plugin, Plugin.ViewerInfo viewer) {
            if (ImGui.BeginPopup($"ContextMenu_{viewer.Name}")) {
                bool showTell = Plugin.Configuration.TellOption;
                bool showEmote = Plugin.Configuration.DoteOption && viewer.isLoaded;
                bool showStalk = Plugin.Configuration.StalkOption && viewer.isLoaded;
                bool showContext = Plugin.Configuration.SearchInfoOption && viewer.isLoaded;
                bool showSearchInfo = Plugin.Configuration.ViewSearchInfoOption && !string.IsNullOrWhiteSpace(viewer.Name);
                bool showPlate = Plugin.Configuration.AdventurePlateOption && (viewer.cid != 0 || viewer.isLoaded);
                bool showExamine = Plugin.Configuration.ExamineOption && viewer.isLoaded;

                bool drewFirstGroup = false;

                if (showTell && IconMenuItem(FontAwesomeIcon.Comment, "Send Tell")) {
                    Plugin.OpenMessageWindow(viewer);
                }
                drewFirstGroup |= showTell;

                if (showEmote && IconMenuItem(FontAwesomeIcon.Smile, "Use Emote")) {
                    EmoteWindowHost.Open(Plugin, viewer);
                }
                drewFirstGroup |= showEmote;

                if (viewer.isLoaded && DrawBuffMenuItem(Plugin, viewer)) {
                    drewFirstGroup = true;
                }

                if (showStalk && IconMenuItem(FontAwesomeIcon.Eye, "Stalk")) {
                    Plugin.OpenStalkWindow(viewer);
                }
                drewFirstGroup |= showStalk;

                if (drewFirstGroup && (showContext || showSearchInfo || showPlate || showExamine)) {
                    ImGui.Dummy(new Vector2(0, 14f * ImGuiHelpers.GlobalScale));
                }

                if (showContext && IconMenuItem(FontAwesomeIcon.Bars, "Open Context Menu")) {
                    OpenGameContextMenu(viewer);
                }

                if (showSearchInfo && IconMenuItem(FontAwesomeIcon.InfoCircle, "View Search Info")) {
                    Plugin.OpenSearchInfo(viewer);
                }

                if (showPlate && IconMenuItem(FontAwesomeIcon.IdCard, "View Adventurer Plate")) {
                    if (viewer.cid != 0) {
                        unsafe {
                            Svc.Framework.RunOnTick(() => {
                                AgentCharaCard.Instance()->OpenCharaCard(viewer.cid);
                            });
                        }
                    } else {
                        IGameObject? character = Svc.Objects.SearchById(viewer.lastKnownGameObjectId);
                        if (character != null) {
                            unsafe {
                                Svc.Framework.RunOnTick(() => {
                                    AgentCharaCard.Instance()->OpenCharaCard(character.Struct());
                                });
                            }
                        }
                    }
                }

                if (showExamine && IconMenuItem(FontAwesomeIcon.Search, "Examine")) {
                    ExamineViewer(viewer);
                }

                ImGui.EndPopup();
            }

            DrawBuffModal(Plugin);
        }

        public static void ShowBuffs(string owner, List<MoodlesStatusInfo> buffs) {
            BuffOwner = owner;
            Buffs = buffs;
            OpenBuffModal = true;
        }
        
        private static bool DrawBuffMenuItem(Plugin Plugin, Plugin.ViewerInfo viewer) {
            if (!Plugin.Configuration.BuffInfoOption) {
                return false;
            }

            if (!Plugin.HasMoodlesIpc()) {
                return false;
            }

            IGameObject? character = Svc.Objects.SearchById(viewer.lastKnownGameObjectId);
            if (character is not IPlayerCharacter player) {
                return false;
            }

            List<MoodlesStatusInfo> buffs;

            try {
                buffs = Plugin.TryGetMoodlesStatus(player);
            } catch {
                return false;
            }

            if (buffs.Count == 0) {
                return false;
            }

            if (IconMenuItem(FontAwesomeIcon.Magic, $"View Buffs ({buffs.Count})")) {
                BuffOwner = viewer.Name;
                Buffs = buffs;
                OpenBuffModal = true;
            }

            return true;
        }

        public static void DrawBuffModal(Plugin Plugin) {
            int frame = ImGui.GetFrameCount();
            if (LastBuffModalDrawFrame == frame) {
                return;
            }

            LastBuffModalDrawFrame = frame;
            if (OpenBuffModal) {
                ImGui.OpenPopup(BuffModalId);
                OpenBuffModal = false;
            }

            if (!ImGui.BeginPopupModal($"Buffs - {BuffOwner}{BuffModalId}", ImGuiWindowFlags.AlwaysAutoResize)) {
                return;
            }

            float scale = ImGuiHelpers.GlobalScale;

            if (Buffs.Count == 0) {
                ImGui.TextDisabled("No Moodles buffs found.");
            } else {
                float height = Math.Min(420f, Math.Max(110f, Buffs.Count * 100f)) * scale;
                ImGui.BeginChild("MoodlesBuffList", new Vector2(520f * scale, height), false);
                for (int i = 0; i < Buffs.Count; i++) {
                    DrawBuffEntry(Buffs[i]);
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();

            var closeSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight() * 1.5f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.50f, 0.22f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.60f, 0.28f, 0.28f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.42f, 0.18f, 0.18f, 1f));
            bool close;
            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                close = ImGui.Button(FontAwesomeIcon.Times.ToIconString() + "##closeBuffs", closeSize);
            }
            
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Close");
            }

            if (close) {
                Buffs = new List<MoodlesStatusInfo>();
                BuffOwner = string.Empty;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private static void DrawBuffEntry(MoodlesStatusInfo buff) {
            string title = CleanMoodlesText(buff.Title);
            if (string.IsNullOrWhiteSpace(title)) {
                title = "(Untitled Moodle)";
            }

            string description = CleanMoodlesText(buff.Description);
            var color = GetBuffTypeColor(buff.Type);

            var style = ImGui.GetStyle();
            var pad = new Vector2(style.FramePadding.X * 1.5f, style.FramePadding.Y * 1.5f);
            float width = ImGui.GetContentRegionAvail().X;
            var drawList = ImGui.GetWindowDrawList();
            var start = ImGui.GetCursorScreenPos();

            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(1);

            ImGui.BeginGroup();
            ImGui.Dummy(new Vector2(0, pad.Y));
            ImGui.Indent(pad.X);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - pad.X * 2f);

            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                ImGui.TextColored(color, GetBuffTypeIcon(buff.Type).ToIconString());
            }
            
            ImGui.SameLine();
            ImGui.TextColored(color, title);

            if (buff.Stacks > 1) {
                ImGui.SameLine();
                ImGui.TextDisabled($"x{buff.Stacks}");
            }

            if (!string.IsNullOrWhiteSpace(description)) {
                ImGui.Spacing();
                ImGui.TextWrapped(description);
            }

            if (!string.IsNullOrWhiteSpace(buff.Applier) || !string.IsNullOrWhiteSpace(buff.CustomVFXPath)) {
                ImGui.Spacing();
            }

            if (!string.IsNullOrWhiteSpace(buff.Applier)) {
                ImGui.TextDisabled($"Applied by: {CleanMoodlesText(buff.Applier)}");
            }

            if (!string.IsNullOrWhiteSpace(buff.CustomVFXPath)) {
                ImGui.TextDisabled($"VFX: {buff.CustomVFXPath}");
            }

            ImGui.PopTextWrapPos();
            ImGui.Unindent(pad.X);
            ImGui.Dummy(new Vector2(0, pad.Y));
            ImGui.EndGroup();

            var end = new Vector2(start.X + width, ImGui.GetItemRectMax().Y);
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(start, end, ImGui.GetColorU32(style.Colors[(int) ImGuiCol.FrameBg]), style.FrameRounding);
            drawList.ChannelsMerge();

            ImGui.Spacing();
        }

        private static FontAwesomeIcon GetBuffTypeIcon(StatusType type) {
            return type switch {
                StatusType.Positive => FontAwesomeIcon.PlusCircle,
                StatusType.Negative => FontAwesomeIcon.MinusCircle,
                StatusType.Special => FontAwesomeIcon.Star,
                _ => FontAwesomeIcon.Circle
            };
        }

        private static Vector4 GetBuffTypeColor(StatusType type) {
            return type switch {
                StatusType.Positive => new Vector4(0.45f, 0.9f, 0.45f, 1f),
                StatusType.Negative => new Vector4(1f, 0.35f, 0.35f, 1f),
                StatusType.Special => new Vector4(0.55f, 0.75f, 1f, 1f),
                _ => new Vector4(1f, 1f, 1f, 1f)
            };
        }
    }
}
