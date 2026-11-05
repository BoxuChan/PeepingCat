using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using System.Numerics;

namespace PeepingCat.Windows {
    public class MessageWindow : Window {
        private Plugin Plugin;
        private string message = string.Empty;
        private Plugin.ViewerInfo Viewer;
        private bool focusInput;
        private const string WindowId = "###PeepingCatMessage";

        public MessageWindow(Plugin plugin, Plugin.ViewerInfo viewer) : base($"Message - {viewer.Name}{WindowId}", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse) {
            Plugin = plugin;
            Viewer = viewer;
            IsOpen = true;
            focusInput = true;
            ShowCloseButton = false;
            RespectCloseHotkey = true;

            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(250, 0),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Show(Plugin.ViewerInfo viewer) {
            bool samePlayer = Viewer.Name == viewer.Name && Viewer.World == viewer.World;
            Viewer = viewer;
            WindowName = $"Message - {viewer.Name}{WindowId}";

            if (!samePlayer) {
                message = string.Empty;
            }

            focusInput = true;
            IsOpen = true;
        }

        public override void Draw() {
            if (ImGui.IsWindowAppearing() || focusInput) {
                ImGui.SetKeyboardFocusHere();
                focusInput = false;
            }

            ImGui.PushItemWidth(-1);

            if (ImGui.InputTextWithHint("##messageInput", "Type your message...", ref message, 1024, ImGuiInputTextFlags.EnterReturnsTrue)) {
                AttemptSendMessage();
            }

            ImGui.PopItemWidth();
            ImGui.Spacing();

            float spacing = ImGui.GetStyle().ItemSpacing.X;
            var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - spacing) / 2f, ImGui.GetFrameHeight() * 1.5f);
            var disabledGrey = new Vector4(0.20f, 0.20f, 0.20f, 1f);

            bool canSend = !string.IsNullOrEmpty(message);
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

            if (send) {
                AttemptSendMessage();
            }
            if (cancel) {
                IsOpen = false;
            }
        }

        private void AttemptSendMessage() {
            if (!string.IsNullOrEmpty(message)) {
                Plugin.OpenChatWith(Viewer, message);
                message = string.Empty;
                IsOpen = false;
            } else {
                Plugin.ChatGui.PrintError("Please enter a message before sending.");
            }
        }
    }
}
