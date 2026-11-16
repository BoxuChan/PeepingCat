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
        private readonly TitleBarButton nearbyButton;
        private readonly TitleBarButton minionButton;
        private readonly TitleBarButton petButton;
        private bool cogAdded = true;
        private bool minionAdded;
        private bool petAdded;

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
                Priority = 4,
                ShowTooltip = () => ImGui.SetTooltip("Opens Config")
            };
            
            nearbyButton = new TitleBarButton {
                Click = (m) => { Plugin.ToggleNearbyWindow(); },
                Icon = FontAwesomeIcon.Eye,
                Priority = 3,
                ShowTooltip = () => ImGui.SetTooltip("Opens Nearby Players")
            };

            minionButton = new TitleBarButton {
                Click = (m) => { Plugin.OpenOwnMinionStalkWindow(); },
                Icon = FontAwesomeIcon.Paw,
                Priority = 2,
                ShowTooltip = () => ImGui.SetTooltip("Stalk your minion")
            };

            petButton = new TitleBarButton {
                Click = (m) => { Plugin.OpenOwnPetStalkWindow(); },
                Icon = FontAwesomeIcon.Dragon,
                Priority = 1,
                ShowTooltip = () => ImGui.SetTooltip("Stalk your pet")
            };

            TitleBarButtons.Add(cogButton);
            TitleBarButtons.Add(nearbyButton);
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

            bool showCompanionButtons = cfg.StalkOption && cfg.StalkOwnMinionPet;
            SyncCompanionButton(minionButton, ref minionAdded, showCompanionButtons && Plugin.HasOwnMinion());
            SyncCompanionButton(petButton, ref petAdded, showCompanionButtons && Plugin.HasOwnPet());
        }

        private void SyncCompanionButton(TitleBarButton button, ref bool added, bool show) {
            if (show && !added) {
                TitleBarButtons.Add(button);
                added = true;
            } else if (!show && added) {
                TitleBarButtons.Remove(button);
                added = false;
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
