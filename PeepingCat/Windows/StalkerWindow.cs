using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using System.Numerics;

namespace PeepingCat.Windows {
    public class StalkerWindow : Window {
        private const string HelpText = "Left-Click: Target this Character\nRight-Click: Open Context Menu";

        private Plugin Plugin;
        private Plugin.ViewerInfo user;

        public StalkerWindow(Plugin plugin, Plugin.ViewerInfo user) : base(user.Name, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse) {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(225, 240),
                MaximumSize = new Vector2(225, 300)
            };

            Size = new Vector2(225, 260);
            SizeCondition = ImGuiCond.FirstUseEver;

            Plugin = plugin;
            this.user = user;
            IsOpen = true;
        }

        public override void OnClose() {
            Plugin.CloseStalkerWindow(user);
            base.OnClose();
        }

        public override void Draw() {
            var allViewers = Plugin.GetAllViewers();
            var style = ImGui.GetStyle();

            DrawHeader("Targeting");
            float targetBoxHeight = ImGui.GetTextLineHeightWithSpacing() + style.FramePadding.Y * 2;

            if (ImGui.BeginListBox("##stalkerTarget", new Vector2(-1, targetBoxHeight))) {
                var target = Plugin.GetStalkerTarget(user);
                if (target != null) {
                    ViewerRow.Draw(Plugin, target, allViewers);
                } else {
                    ImGui.TextDisabled("Is not focusing anyone.");
                }

                ImGui.EndListBox();
            }

            ImGui.Spacing();

            DrawHeader("Current Viewers");
            float height = ImGui.GetContentRegionAvail().Y - style.ItemSpacing.Y;

            if (ImGui.BeginListBox("##stalkerViewers", new Vector2(-1, height))) {
                var stalkerViewers = Plugin.GetStalkerViewers(user);
                if (stalkerViewers.Count > 0) {
                    foreach (var viewer in stalkerViewers) {
                        ViewerRow.Draw(Plugin, viewer, allViewers);
                    }
                } else {
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
    }
}
