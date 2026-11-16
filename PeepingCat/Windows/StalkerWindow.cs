using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using System.Numerics;
using System;

namespace PeepingCat.Windows {
    public class StalkerWindow : Window {
        private const string HelpText = "Left-Click: Target this Character\nRight-Click: Open Context Menu";

        private Plugin Plugin;
        private Plugin.ViewerInfo? user;
        private string? objectKey;

        public StalkerWindow(Plugin plugin, Plugin.ViewerInfo user) : this(plugin, user.Name) {
            this.user = user;
        }

        public StalkerWindow(Plugin plugin, string objectKey, string title) : this(plugin, $"{title}###ObjStalk_{objectKey}") {
            this.objectKey = objectKey;
        }

        private StalkerWindow(Plugin plugin, string windowName) : base(windowName, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse) {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(225, 240),
                MaximumSize = new Vector2(225, 300)
            };

            Size = new Vector2(225, 260);
            SizeCondition = ImGuiCond.FirstUseEver;

            Plugin = plugin;
            IsOpen = true;
        }

        public override void OnClose() {
            if (objectKey != null) {
                var stalk = Plugin.GetObjectStalk(objectKey);
                if (stalk != null && stalk.IsOwn && Plugin.Configuration.KeepOwnCompanionHistory) {
                    base.OnClose();
                    return;
                }

                Plugin.CloseObjectStalkWindow(objectKey);
            } else if (user != null) {
                Plugin.CloseStalkerWindow(user);
            }

            base.OnClose();
        }

        public override void Draw() {
            var allViewers = Plugin.GetAllViewers();
            var style = ImGui.GetStyle();

            var objectStalk = objectKey != null ? Plugin.GetObjectStalk(objectKey) : null;
            if (objectKey != null && objectStalk == null) {
                return;
            }

            Plugin.ViewerInfo? target;
            Plugin.ObjectTargetInfo? objectTarget;

            if (objectStalk != null) {
                target = objectStalk.TargetPlayer;
                objectTarget = objectStalk.TargetObject;
            } else {
                target = Plugin.GetStalkerTarget(user!);
                objectTarget = Plugin.GetStalkerObjectTarget(user!);
            }

            bool showTargeting = objectStalk == null || Plugin.IsShownTargetKind(objectStalk.Info.KindLabel);

            if (showTargeting) {
                DrawHeader("Targeting");
                float targetBoxHeight = ImGui.GetTextLineHeightWithSpacing() + style.FramePadding.Y * 2;

                if (ImGui.BeginListBox("##stalkerTarget", new Vector2(-1, targetBoxHeight))) {
                    if (target != null) {
                        ViewerRow.Draw(Plugin, target, allViewers);
                    } else if (objectTarget != null) {
                        DrawObjectTarget(objectTarget);
                    } else if (objectStalk != null && !objectStalk.IsLoaded) {
                        ImGui.TextDisabled("Not loaded.");
                    } else {
                        ImGui.TextDisabled("Is not focusing anyone.");
                    }

                    ImGui.EndListBox();
                }

                ImGui.Spacing();
            }

            DrawHeader("Current Viewers");
            float height = ImGui.GetContentRegionAvail().Y - style.ItemSpacing.Y;

            if (ImGui.BeginListBox("##stalkerViewers", new Vector2(-1, height))) {
                var stalkerViewers = objectKey != null ? Plugin.GetObjectStalkViewers(objectKey) : Plugin.GetStalkerViewers(user!);
                if (stalkerViewers.Count > 0) {
                    foreach (var viewer in stalkerViewers) {
                        ViewerRow.Draw(Plugin, viewer, allViewers);
                    }
                } else if (Plugin.Configuration.ShowNoViewersText) {
                    ImGui.Text("No viewers yet.");
                }

                ImGui.EndListBox();
            }
        }

        private void DrawHeader(string title) {
            ImGui.Text(title);
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                ImGui.TextUnformatted(HelpText);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
        
        private void DrawObjectTarget(Plugin.ObjectTargetInfo info) {
            var style = ImGui.GetStyle();
            var drawList = ImGui.GetWindowDrawList();

            string tag = info.KindLabel;
            float tagWidth = ImGui.CalcTextSize(tag).X;

            var start = ImGui.GetCursorScreenPos();
            float rowWidth = ImGui.GetContentRegionAvail().X;
            float maxNameWidth = Math.Max(20f, rowWidth - tagWidth - style.ItemSpacing.X * 2f);

            string label = string.IsNullOrWhiteSpace(info.Title) ? info.Name : $"{info.Name} «{info.Title}»";
            string shown = Truncate(label, maxNameWidth);
            var nameColour = info.TextColour ?? Plugin.Configuration.loadedColour;

            ImGui.Selectable("##objectTarget", false);
            bool hovered = ImGui.IsItemHovered();

            if (info.EdgeColour.HasValue) {
                uint edge = ImGui.GetColorU32(info.EdgeColour.Value);
                drawList.AddText(start + new Vector2(-1, 0), edge, shown);
                drawList.AddText(start + new Vector2(1, 0), edge, shown);
                drawList.AddText(start + new Vector2(0, -1), edge, shown);
                drawList.AddText(start + new Vector2(0, 1), edge, shown);
            }

            drawList.AddText(start, ImGui.GetColorU32(nameColour), shown);
            drawList.AddText(new Vector2(start.X + rowWidth - tagWidth, start.Y), ImGui.GetColorU32(ImGuiCol.TextDisabled), tag);

            if (hovered) {
                string displayName = string.IsNullOrEmpty(info.OriginalName) ? info.Name : info.OriginalName;
                string tip = $"{info.KindLabel}: {displayName}";
                if (!string.IsNullOrEmpty(info.Title)) {
                    tip += $"\nTitle: {info.Title}";
                }
                if (!string.IsNullOrEmpty(info.OwnerName)) {
                    tip += $"\nOwner: {info.OwnerName}";
                }
                ImGui.SetTooltip(tip);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                    Plugin.TargetObject(info);
                    ImGui.SetWindowFocus(ImU8String.Empty);
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && (Plugin.Configuration.SearchInfoOption || Plugin.Configuration.StalkOption)) {
                    ImGui.OpenPopup("ObjectContextMenu");
                }
            }

            if (ImGui.BeginPopup("ObjectContextMenu")) {
                if (Plugin.Configuration.StalkOption && Popup.IconMenuItem(FontAwesomeIcon.Eye, "Stalk")) {
                    Plugin.OpenObjectStalkWindow(info);
                }

                if (Plugin.Configuration.SearchInfoOption && Popup.IconMenuItem(FontAwesomeIcon.Bars, "Open Context Menu")) {
                    Plugin.OpenObjectContextMenu(info);
                }

                ImGui.EndPopup();
            }
        }

        private static string Truncate(string text, float maxWidth) {
            if (ImGui.CalcTextSize(text).X <= maxWidth) {
                return text;
            }

            const string ellipsis = "...";
            for (int len = text.Length - 1; len > 0; len--) {
                string candidate = text.Substring(0, len).TrimEnd() + ellipsis;
                if (ImGui.CalcTextSize(candidate).X <= maxWidth) {
                    return candidate;
                }
            }

            return ellipsis;
        }
    }
}
