using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;

using ECommons.DalamudServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PeepingCat.Windows {
    public class NearbyWindow : Window {
        private const long ScanIntervalMs = 500;

        private readonly Plugin plugin;
        private readonly Dictionary<string, Plugin.ViewerInfo> nearby = new();
        private readonly HashSet<Plugin.ViewerInfo> withMoodles = new();
        private List<Plugin.ViewerInfo> sorted = new();
        private long lastScan;

        public NearbyWindow(Plugin plugin) : base("Peeping Cat - Nearby Players") {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(225, 200),
                MaximumSize = new Vector2(600, 800)
            };
            Size = new Vector2(250, 300);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override void OnClose() {
            nearby.Clear();
            withMoodles.Clear();
            sorted = new List<Plugin.ViewerInfo>();
            lastScan = 0;
        }

        private bool HasMoodles(IPlayerCharacter player) {
            try {
                return plugin.TryGetMoodlesStatus(player).Count > 0;
            } catch {
                return false;
            }
        }

        private void Scan() {
            var local = Svc.Objects.LocalPlayer;
            withMoodles.Clear();

            if (local == null) {
                nearby.Clear();
                sorted = new List<Plugin.ViewerInfo>();
                return;
            }

            bool checkMoodles = plugin.Configuration.ColourMoodlesNearby && plugin.HasMoodlesIpc();
            var seen = new HashSet<string>();

            foreach (var obj in Svc.Objects) {
                if (obj is not IPlayerCharacter player || player.GameObjectId == local.GameObjectId) {
                    continue;
                }

                string key = plugin.GetPlayerKey(player);
                seen.Add(key);

                if (!nearby.TryGetValue(key, out var viewer)) {
                    viewer = plugin.CreateViewer(player);
                    viewer.IsActive = false;
                    nearby[key] = viewer;
                } else {
                    viewer.lastKnownGameObjectId = player.GameObjectId;
                }

                if (checkMoodles && HasMoodles(player)) {
                    withMoodles.Add(viewer);
                }
            }

            foreach (var key in nearby.Keys.Where(k => !seen.Contains(k)).ToList()) {
                nearby.Remove(key);
            }

            sorted = nearby.Values
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override void Draw() {
            long now = Environment.TickCount64;
            if (now - lastScan >= ScanIntervalMs) {
                lastScan = now;
                Scan();
            }

            var allViewers = plugin.GetAllViewers();
            var moodlesColour = plugin.Configuration.moodlesColour;
            float height = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y;

            if (ImGui.BeginListBox("##nearby", new Vector2(-1, height))) {
                if (sorted.Count > 0) {
                    foreach (var viewer in sorted) {
                        Vector4? colour = withMoodles.Contains(viewer) ? moodlesColour : null;
                        ViewerRow.Draw(plugin, viewer, allViewers, false, colour);
                    }
                } else {
                    ImGui.TextDisabled("Nobody nearby.");
                }

                ImGui.EndListBox();
            }
        }
    }
}
