using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

using ECommons.DalamudServices;

using System.Numerics;

namespace PeepingCat.Windows {
    public class MainWindow : Window {
        private Plugin Plugin;
        private readonly TitleBarButton cogButton;
        private bool cogAdded = true;

        public MainWindow(Plugin plugin) : base("Peeping Cat", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse) {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(140, 130),
                MaximumSize = new Vector2(1000, 500)
            };

            Size = new Vector2(225, 130);
            SizeCondition = ImGuiCond.FirstUseEver;
            Plugin = plugin;

            cogButton = new TitleBarButton {
                Click = (m) => { Plugin.DrawConfig(); },
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGui.SetTooltip("Opens Config")
            };

            TitleBarButtons.Add(cogButton);
        }

        public override bool DrawConditions() {
            var cfg = Plugin.Configuration;

            if (!cfg.ShowInCombat && Svc.Condition[ConditionFlag.InCombat]) {
                return false;
            }

            if (cfg.HideWindowWhileInDuty && (Svc.Condition[ConditionFlag.BoundByDuty] || Svc.Condition[ConditionFlag.BoundByDuty56] || Svc.Condition[ConditionFlag.BoundByDuty95])) {
                return false;
            }

            if (!cfg.ShowInCutscenes && (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.WatchingCutscene78])) {
                return false;
            }

            return true;
        }

        public override void PreDraw() {
            var cfg = Plugin.Configuration;

            var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            if (!cfg.AllowMovingMainWindow) {
                flags |= ImGuiWindowFlags.NoMove;
            }
            if (!cfg.AllowResizingMainWindow) {
                flags |= ImGuiWindowFlags.NoResize;
            }

            Flags = flags;
            RespectCloseHotkey = cfg.AllowClosingWithEscape;

            if (cfg.ShowSettingsButton && !cogAdded) {
                TitleBarButtons.Add(cogButton);
                cogAdded = true;
            } else if (!cfg.ShowSettingsButton && cogAdded) {
                TitleBarButtons.Remove(cogButton);
                cogAdded = false;
            }
        }

        public override void Draw() {
            var allViewers = Plugin.GetAllViewers();
            var viewers = Plugin.GetViewers();
            float height = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y;

            if (ImGui.BeginListBox("##viewers", new Vector2(-1, height))) {
                if (viewers.Count > 0) {
                    foreach (var viewer in viewers) {
                        ViewerRow.Draw(Plugin, viewer, allViewers);
                    }
                } else if (Plugin.Configuration.ShowNoViewersText) {
                    ImGui.Text("No viewers yet.");
                }

                ImGui.EndListBox();
            }
        }
    }
}
