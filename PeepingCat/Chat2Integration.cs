using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using ECommons.DalamudServices;

using PeepingCat.Windows;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace PeepingCat;

public sealed class Chat2Integration : IDisposable {
    private readonly Plugin plugin;

    private readonly ICallGateSubscriber<string> register;
    private readonly ICallGateSubscriber<string, object?> unregister;
    private readonly ICallGateSubscriber<object?> available;
    private readonly ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> invoke;

    private string? id;
    private bool enabled;

    public Chat2Integration(Plugin plugin, IDalamudPluginInterface pluginInterface) {
        this.plugin = plugin;

        register = pluginInterface.GetIpcSubscriber<string>("ChatTwo.Register");
        unregister = pluginInterface.GetIpcSubscriber<string, object?>("ChatTwo.Unregister");
        available = pluginInterface.GetIpcSubscriber<object?>("ChatTwo.Available");
        invoke = pluginInterface.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");
    }

    public void Enable() {
        if (enabled) {
            return;
        }

        enabled = true;
        available.Subscribe(Register);
        invoke.Subscribe(OnInvoke);
        Register();
    }

    public void Disable() {
        if (!enabled) {
            return;
        }

        enabled = false;
        available.Unsubscribe(Register);
        invoke.Unsubscribe(OnInvoke);

        if (id != null) {
            try {
                unregister.InvokeAction(id);
            } catch { }
            
            id = null;
        }
    }

    private void Register() {
        try {
            id = register.InvokeFunc();
        } catch {
            id = null;
        }
    }

    private void OnInvoke(string callId, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content) {
        if (!enabled || id == null || callId != id || sender == null) {
            return;
        }

        var cfg = plugin.Configuration;
        if (!cfg.Chat2MenuOption) {
            return;
        }

        try {
            string name = sender.PlayerName;
            string world = plugin.GetWorldName(sender.World.RowId);
            var nearby = FindNearby(name, world);

            var viewer = nearby != null ? plugin.CreateViewer(nearby) : new Plugin.ViewerInfo { Name = name, World = world, cid = contentId };

            bool showEmote = cfg.DoteOption && nearby != null;
            bool showStalk = cfg.StalkOption && nearby != null;
            bool showContext = cfg.SearchInfoOption && nearby != null;
            bool showSearchInfo = cfg.ViewSearchInfoOption && !string.IsNullOrWhiteSpace(name);
            bool showExamine = cfg.ExamineOption && nearby != null;

            List<MoodlesStatusInfo> buffs = new();
            if (cfg.BuffInfoOption && nearby != null && plugin.HasMoodlesIpc()) {
                try {
                    buffs = plugin.TryGetMoodlesStatus(nearby);
                } catch { }
            }
            bool showBuffs = buffs.Count > 0;

            bool drewFirstGroup = false;

            if (showEmote && Popup.IconMenuItem(FontAwesomeIcon.Smile, "Use Emote")) {
                EmoteWindowHost.Open(plugin, viewer);
            }
            drewFirstGroup |= showEmote;

            if (showBuffs && Popup.IconMenuItem(FontAwesomeIcon.Magic, $"View Buffs ({buffs.Count})")) {
                Popup.ShowBuffs(viewer.Name, buffs);
            }
            drewFirstGroup |= showBuffs;

            if (showStalk && Popup.IconMenuItem(FontAwesomeIcon.Eye, "Stalk")) {
                plugin.OpenStalkWindow(viewer);
            }
            drewFirstGroup |= showStalk;

            if (drewFirstGroup && (showContext || showSearchInfo || showExamine)) {
                ImGui.Dummy(new Vector2(0, 14f * ImGuiHelpers.GlobalScale));
            }

            if (showContext && Popup.IconMenuItem(FontAwesomeIcon.Bars, "Open Context Menu")) {
                Popup.OpenGameContextMenu(viewer);
            }

            if (showSearchInfo && Popup.IconMenuItem(FontAwesomeIcon.InfoCircle, "View Search Info")) {
                plugin.OpenSearchInfo(viewer);
            }
            
            if (showExamine && Popup.IconMenuItem(FontAwesomeIcon.Search, "Examine")) {
                Popup.ExamineViewer(viewer);
            }
        } catch { }
    }

    private IPlayerCharacter? FindNearby(string name, string world) {
        foreach (var obj in Svc.Objects) {
            if (obj is IPlayerCharacter pc && pc.Name.TextValue == name && plugin.GetWorldName(pc.HomeWorld.RowId) == world) {
                return pc; 
                
            }
        }

        return null;
    }

    public void Dispose() => Disable();
}
