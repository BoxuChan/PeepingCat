using Dalamud.Bindings.ImGui;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace PeepingCat.Windows {
    public static class ViewerRow {
        public static void Draw(Plugin plugin, Plugin.ViewerInfo viewer, HashSet<Plugin.ViewerInfo> allViewers, bool showTime = true) {
            var cfg = plugin.Configuration;
            var color = GetColor(plugin, viewer);
            var time = FormatTime(viewer.LastSeen, cfg.Use24HourTime);

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Selectable(viewer.Name, false);

            float windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered()) {
                if (viewer.isLoaded && !viewer.isFocused) {
                    foreach (var v in allViewers) {
                        if (v.isFocused) {
                            plugin.HighlightCharacter(v);
                        }
                    }
                    plugin.HighlightCharacter(viewer);
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                    plugin.TargetCharacter(viewer);
                    ImGui.SetWindowFocus(ImU8String.Empty);
                }
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
                    ImGui.OpenPopup($"ContextMenu_{viewer.Name}");
                }
            } else if (viewer.isFocused) {
                plugin.HighlightCharacter(viewer);
            }

            Popup.Draw(plugin, viewer);

            if (showTime && cfg.ShowTimestamps) {
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);
                ImGui.TextUnformatted(time);
                ImGui.PopStyleColor();
            }
        }

        private static string FormatTime(DateTime seen, bool use24Hour) {
            if (DateTime.Now - seen >= TimeSpan.FromDays(1)) {
                return seen.ToString("dd/MM");
            }

            return use24Hour ? seen.ToString("HH:mm") : seen.ToString("h:mm tt");
        }

        private static Vector4 GetColor(Plugin plugin, Plugin.ViewerInfo viewer) {
            if (viewer.IsActive) {
                return plugin.Configuration.targetingColour;
            }
            return viewer.isLoaded ? plugin.Configuration.loadedColour : plugin.Configuration.unloadedColour;
        }
    }
}
