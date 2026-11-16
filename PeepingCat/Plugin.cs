using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameFunctions;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Companion = FFXIVClientStructs.FFXIV.Client.Game.Character.Companion;
using Lumina.Excel.Sheets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using PeepingCat.Windows;
using PeepingCat.Helpers;

namespace PeepingCat {
    public sealed class Plugin : IDalamudPlugin {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

        private const string CommandName1 = "/pcat";
        private const string CommandName2 = "/peepingcat";
        private const string CommandName3 = "/pcatconfig";
        private const string CommandName4 = "/pcat nearby";

        private Dictionary<string, ViewerInfo> viewers = new();
        private Dictionary<string, ViewerInfo> unknownViewer = new();
        private Dictionary<string, Dictionary<string, ViewerInfo>> stalkerViewers = new();
        private Dictionary<string, StalkerWindow> stalkerWindows = new();
        private Dictionary<string, ViewerInfo?> stalkerLooksAt = new();
        private Dictionary<string, ObjectTargetInfo?> stalkerObjectTarget = new();
        private Dictionary<string, ObjectStalk> objectStalks = new();
        private MessageWindow? messageWindow;
        private bool focusSaved;
        private ulong savedFocusId;

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("Peeping Cat");
        private MainWindow MainWindow { get; init; }
        private ConfigWindow ConfigWindow { get; init; }
        private SoundManager SoundManager { get; init; }

        private readonly Dictionary<uint, string> worldNames = new();

        private long lastUpdateTick;
        private const int UpdateIntervalMs = 100;

        private long lastLoadingTick;
        private const int LoadingIntervalMs = 1500;

        private bool firstDrawn;
        private bool dutyWindowSuppressed;
        private readonly Dictionary<string, long> lastSoundNotificationTicks = new();

        private readonly ICallGateSubscriber<IPlayerCharacter, List<MoodlesStatusInfo>> GetStatusManagerInfoByPlayerV2;
        
        private Chat2Integration chat2Integration = null!;
        private NearbyWindow NearbyWindow = null!;
        private readonly ICallGateSubscriber<string, bool, object> xivMessengerOpen;
        
        private readonly ICallGateSubscriber<bool> petNicknamesEnabled;
        private List<string>? petNames;
        private List<nint>? petAddresses;
        private Dictionary<ulong, string>? petLegacyNames;
        private bool petDataTouched;
        private long lastPetLookupTick;
        private List<Vector3?>? petTextColours;
        private List<Vector3?>? petEdgeColours;
        
        private readonly Dictionary<ulong, (string Title, Vector4? Text, Vector4? Edge)> nameplateCache = new();

        public Plugin() {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            MainWindow = new MainWindow(this);
            ConfigWindow = new ConfigWindow(this);
            SoundManager = new SoundManager(this); 
            NearbyWindow = new NearbyWindow(this);
            
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(NearbyWindow);

            CommandManager.AddHandler(CommandName1, new CommandInfo(OnCommand) {
                HelpMessage = "Opens P-Cat Window."
            });

            CommandManager.AddHandler(CommandName2, new CommandInfo(OnCommand) {
                HelpMessage = "Opens P-Cat Window."
            });

            CommandManager.AddHandler(CommandName3, new CommandInfo(OnConfig) {
                HelpMessage = "Opens P-Cat Config."
            });
            
            CommandManager.AddHandler(CommandName4, new CommandInfo(OnConfig) {
                HelpMessage = "Opens Nearby Players Window."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfig;
            PluginInterface.UiBuilder.OpenMainUi += DrawMain;

            if (Configuration.OpenOnLogin) {
                ClientState.Login += OnLogin;
                ClientState.TerritoryChanged += OnTerritoryChanged;
            }

            var worldSheet = DataManager.GetExcelSheet<World>();
            foreach (var world in worldSheet) {
                worldNames[world.RowId] = world.Name.ExtractText();
            }

            ECommonsMain.Init(PluginInterface, this);
            SoundManager.CheckSoundFile();
            Framework.Update += OnUpdate;
            Framework.Update += OnSearchInfoUpdate;
            GetStatusManagerInfoByPlayerV2 = Svc.PluginInterface.GetIpcSubscriber<IPlayerCharacter, List<MoodlesStatusInfo>>("Moodles.GetStatusManagerInfoByPlayerV2");
            xivMessengerOpen = Svc.PluginInterface.GetIpcSubscriber<string, bool, object>("Messenger.OpenMessenger");
            petNicknamesEnabled = Svc.PluginInterface.GetIpcSubscriber<bool>("PetRenamer.IsEnabled");
            
            chat2Integration = new Chat2Integration(this, PluginInterface);
            if (Configuration.Chat2MenuOption) {
                chat2Integration.Enable();
            }
        }

        private bool IsInDutyInstance() {
            try {
                return Condition[ConditionFlag.BoundByDuty];
            } catch {
                return false;
            }
        }

        public bool ShouldSuppressDutySound() {
            if (ClientState.IsPvP) {
                return Configuration.SuppressSoundWhileInPvP;
            }

            return Configuration.SuppressSoundWhileInDuty && IsInDutyInstance();
        }

        private void HandleDutyWindowSuppression() {
            if (!Configuration.HideWindowWhileInDuty) {
                if (dutyWindowSuppressed) {
                    if (!IsMainWindowOpen()) {
                        MainWindow.IsOpen = true;
                    }
                    
                    dutyWindowSuppressed = false;
                }
                
                return;
            }

            bool inDuty = IsInDutyInstance();

            if (inDuty) {
                if (IsMainWindowOpen()) {
                    MainWindow.IsOpen = false;
                    dutyWindowSuppressed = true;
                }
            } else if (dutyWindowSuppressed) {
                if (!IsMainWindowOpen()) {
                    MainWindow.IsOpen = true;
                }
                
                dutyWindowSuppressed = false;
            }
        }

        public void Dispose() {
            WindowSystem.RemoveAllWindows();

            ClientState.Login -= OnLogin;
            ClientState.TerritoryChanged -= OnTerritoryChanged;

            CommandManager.RemoveHandler(CommandName1);
            CommandManager.RemoveHandler(CommandName2);
            CommandManager.RemoveHandler(CommandName3);
            CommandManager.RemoveHandler(CommandName4);

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfig;
            PluginInterface.UiBuilder.OpenMainUi -= DrawMain;

            Framework.Update -= OnUpdate;
            Framework.Update -= OnSearchInfoUpdate;
            
            chat2Integration.Dispose();
            
            unsafe {
                if (searchInfoData != null) {
                    NativeMemory.Free(searchInfoData);
                    searchInfoData = null;
                }
            }

            if (petDataTouched) {
                foreach (var tag in new[] { "PetRenamer.PetNicknamesStrings", "PetRenamer.PetNicknamesAddresses", "PetRenamer.GameObjectRenameDict", "PetRenamer.PetNicknamesTextColours", "PetRenamer.PetNicknamesEdgeColours" }) {
                    try {
                        PluginInterface.RelinquishData(tag);
                    } catch { }
                }
            }
            
            ECommonsMain.Dispose();
        }

        private void OnCommand(string command, string args) {
            if (args.Trim().Equals("nearby", StringComparison.OrdinalIgnoreCase)) {
                ToggleNearbyWindow();
                return;
            }

            MainWindow.IsOpen = !MainWindow.IsOpen;
        }

        private void OnConfig(string command, string args) {
            ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        }

        private void OnLogin() {
            TryOpenMainWindow();
        }

        private void OnTerritoryChanged(uint territoryId) {
            TryOpenMainWindow();
        }

        private void TryOpenMainWindow() {
            if (ClientState.IsLoggedIn && ObjectTable.LocalPlayer != null && !firstDrawn) {
                ClientState.Login -= OnLogin;
                ClientState.TerritoryChanged -= OnTerritoryChanged;
                firstDrawn = true;
                MainWindow.IsOpen = true;
            }
        }

        private void DrawUI() {
            WindowSystem.Draw();
            Popup.DrawBuffModal(this);
            DrawMarkers();
        }

        private void DrawMarkers() {
            if (!Configuration.MarkTargeted && !Configuration.MarkTargeting) {
                return;
            }

            if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer == null) {
                return;
            }

            if (Condition[ConditionFlag.WatchingCutscene] || Condition[ConditionFlag.WatchingCutscene78] || Condition[ConditionFlag.OccupiedInCutSceneEvent]) {
                return;
            }

            if (Configuration.MarkTargeted && TargetManager.Target is IPlayerCharacter target) {
                MarkPlayer(target, Configuration.MarkTargetedColour, Configuration.MarkTargetedSize);
            }

            if (Configuration.MarkTargeting) {
                foreach (var viewer in viewers.Values) {
                    if (!viewer.IsActive || !viewer.isLoaded) {
                        continue;
                    }

                    if (ObjectTable.SearchById(viewer.lastKnownGameObjectId) is IPlayerCharacter pc) {
                        MarkPlayer(pc, Configuration.MarkTargetingColour, Configuration.MarkTargetingSize);
                    }
                }
            }
        }

        private static void MarkPlayer(IGameObject obj, Vector4 colour, float size) {
            if (!GameGui.WorldToScreen(obj.Position, out var screenPos)) {
                return;
            }

            var pos = ImGuiHelpers.MainViewport.Pos + screenPos;
            ImGui.GetBackgroundDrawList().AddCircleFilled(pos, size, ImGui.GetColorU32(colour), 32);
        }
        
        public void DrawConfig() => OnConfig("/pcatconfig", "");
        private void DrawMain() => OnCommand("/pcat", "");
        public void ToggleNearbyWindow() => NearbyWindow.IsOpen = !NearbyWindow.IsOpen;

        public bool HasMoodlesIpc() {
            return GetStatusManagerInfoByPlayerV2.HasFunction;
        }

        public void SetChat2Integration(bool enabled) {
            if (enabled) {
                chat2Integration.Enable();
            } else {
                chat2Integration.Disable();
            }
        }
        
        public List<MoodlesStatusInfo> TryGetMoodlesStatus(IPlayerCharacter player) {
            if (!GetStatusManagerInfoByPlayerV2.HasFunction) {
                return new List<MoodlesStatusInfo>();
            }

            return GetStatusManagerInfoByPlayerV2.InvokeFunc(player);
        }

        public void CloseStalkerWindow(ViewerInfo viewer) {
            string stalkerKey = GetViewerKey(viewer);

            unknownViewer.Remove(stalkerKey);
            stalkerViewers.Remove(stalkerKey);
            stalkerLooksAt.Remove(stalkerKey);
            stalkerObjectTarget.Remove(stalkerKey);

            if (stalkerWindows.Remove(stalkerKey, out var window)) {
                WindowSystem.RemoveWindow(window);
            }
        }

        public void OpenStalkWindow(ViewerInfo viewer) {
            string stalkerKey = GetViewerKey(viewer);
            
            if (stalkerViewers.ContainsKey(stalkerKey)) {
                return;
            }

            if (!viewers.ContainsKey(stalkerKey)) {
                unknownViewer.Add(stalkerKey, viewer);
            }

            stalkerViewers[stalkerKey] = new Dictionary<string, ViewerInfo>();
            stalkerLooksAt[stalkerKey] = null;
            stalkerObjectTarget[stalkerKey] = null;
            var stalkerWindow = new StalkerWindow(this, viewer);
            WindowSystem.AddWindow(stalkerWindow);
            stalkerWindows[stalkerKey] = stalkerWindow;
        }

        public List<ViewerInfo> GetStalkerViewers(ViewerInfo user) {
            var result = new List<ViewerInfo>();
            string stalkerKey = GetViewerKey(user);

            if (!stalkerViewers.ContainsKey(stalkerKey)) {
                return result;
            }

            foreach (var viewer in stalkerViewers[stalkerKey].Values) {
                result.Add(viewer);
            }

            return result.OrderByDescending(viewer => viewer.IsActive).ThenByDescending(viewer => viewer.LastSeen) .Take(15).ToList();
        }

        public ViewerInfo? GetStalkerTarget(ViewerInfo stalker) {
            string stalkerKey = GetViewerKey(stalker);
            if (stalkerLooksAt.TryGetValue(stalkerKey, out var targetKey)) {
                return targetKey;
            }
            
            return null;
        }

        private bool PassesFilters(IPlayerCharacter character) {
            var flags = character.StatusFlags;

            if (!Configuration.LogParty && flags.HasFlag(StatusFlags.PartyMember)) {
                return false;
            }
            
            if (!Configuration.LogAlliance && flags.HasFlag(StatusFlags.AllianceMember)) {
                return false;
            }
            
            if (!Configuration.LogInCombat && flags.HasFlag(StatusFlags.InCombat)) {
                return false;
            }

            return true;
        }
        
        private void OnUpdate(IFramework framework) {
            HandleDutyWindowSuppression();

            long currentTick = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentTick - lastUpdateTick < UpdateIntervalMs) {
                return;
            }
            
            lastUpdateTick = currentTick;

            if (currentTick - lastLoadingTick >= LoadingIntervalMs) {
                lastLoadingTick = currentTick;
                var allKnownViewers = GetAllViewers();
                var loadedPcs = FindLoadedViewersInObjectTable(allKnownViewers);
                
                foreach (var viewer in allKnownViewers) {
                    string key = GetViewerKey(viewer);
                    if (!loadedPcs.ContainsKey(key)) {
                        viewer.isLoaded = false;
                    } else {
                        viewer.isLoaded = true;
                        viewer.lastKnownGameObjectId = loadedPcs[key];
                    }
                }
            }

            if (ObjectTable.LocalPlayer == null) {
                return;
            }
            
            ulong localPlayerId = ObjectTable.LocalPlayer.GameObjectId;
            var stalkedIds = new Dictionary<ulong, string>();
            foreach (var viewer in stalkerViewers) {
                if (viewers.TryGetValue(viewer.Key, out var stalker) && stalker.isLoaded) {
                    stalkedIds[stalker.lastKnownGameObjectId] = viewer.Key;
                } else if (unknownViewer.TryGetValue(viewer.Key, out var stalker2) && stalker2.isLoaded) {
                    stalkedIds[stalker2.lastKnownGameObjectId] = viewer.Key;
                }
            }

            var currentlyLookingAtMe = new HashSet<string>();
            var currentlyLookingAtStalker = new Dictionary<string, HashSet<string>>();

            foreach (var stKey in stalkerViewers.Keys) {
                currentlyLookingAtStalker[stKey] = new HashSet<string>();
            }

            bool windowOpen = IsMainWindowOpen();
            
            foreach (var obj in ObjectTable) {
                if (obj is IPlayerCharacter character) {
                    bool isSelf = character.GameObjectId == localPlayerId;
                    bool targetsMe;

                    if (isSelf) {
                        targetsMe = Configuration.DetectSelfTarget && (character.TargetObjectId == localPlayerId || TargetManager.Target?.GameObjectId == localPlayerId);
                    } else {
                        targetsMe = character.TargetObjectId == localPlayerId && PassesFilters(character);
                    }

                    if (targetsMe) {
                        string charKey = GetPlayerKey(character);
                        currentlyLookingAtMe.Add(charKey);

                        if (!viewers.TryGetValue(charKey, out var viewer)) {
                            viewer = CreateViewer(character);
                            viewer.SeenWhileClosed = !windowOpen;
                            viewers[charKey] = viewer;
                        } else {
                            viewer.IsActive = true;
                            viewer.LastSeen = DateTime.Now;
                            
                            if (windowOpen) {
                                viewer.SeenWhileClosed = false;
                            }
                        }

                        try {
                            TryPlayViewerSound(viewer, currentTick);
                        } catch { }
                    }

                    if (stalkedIds.TryGetValue(character.TargetObjectId, out var stalker)) {
                        string charKey = GetPlayerKey(character);
                        currentlyLookingAtStalker[stalker].Add(charKey);

                        if (!stalkerViewers[stalker].TryGetValue(charKey, out var stalkerInfo)) {
                            stalkerViewers[stalker][charKey] = CreateViewer(character);
                        } else {
                            stalkerInfo.IsActive = true;
                            stalkerInfo.LastSeen = DateTime.Now;
                        }
                    }

                    if (stalkedIds.TryGetValue(character.GameObjectId, out var stalker2)) {
                        var targetObject = ObjectTable.SearchById(character.TargetObjectId);

                        if (targetObject == null) {
                            stalkerLooksAt[stalker2] = null;
                            stalkerObjectTarget[stalker2] = null;
                        } else if (targetObject is IPlayerCharacter possibleTarget) {
                            stalkerObjectTarget[stalker2] = null;

                            if (stalkerLooksAt[stalker2] != null) {
                                if (GetViewerKey(stalkerLooksAt[stalker2]!) != $"{possibleTarget.Name.TextValue}@{GetWorldName(possibleTarget.HomeWorld.RowId)}") {
                                    stalkerLooksAt[stalker2] = CreateViewer(possibleTarget);
                                }
                            } else {
                                stalkerLooksAt[stalker2] = CreateViewer(possibleTarget);
                            }
                        } else {
                            stalkerLooksAt[stalker2] = null;
                            stalkerObjectTarget[stalker2] = BuildObjectInfo(targetObject);
                        }
                    }
                }
            }

            foreach (var viewerInfo in viewers.Values) {
                string key = GetViewerKey(viewerInfo);
                if (!currentlyLookingAtMe.Contains(key)) {
                    viewerInfo.IsActive = false;
                    viewerInfo.soundPlayed = false;
                }
            }

            foreach (var key in stalkerViewers) {
                foreach (var stalker in key.Value) {
                    if (!currentlyLookingAtStalker[key.Key].Contains(stalker.Key)) {
                        stalker.Value.IsActive = false;
                    }
                }
            }

            UpdateObjectStalks(currentTick);
        }

        private void TryPlayViewerSound(ViewerInfo viewer, long currentTick) {
            if (!Configuration.SoundEnabled || viewer.soundPlayed || ShouldSuppressDutySound()) {
                return;
            }

            string viewerKey = GetViewerKey(viewer);
            int cooldownMs = Math.Clamp(Configuration.SoundCooldownSeconds, 0, 3600) * 1000;

            if (lastSoundNotificationTicks.TryGetValue(viewerKey, out var lastTick) && currentTick - lastTick < cooldownMs) {
                viewer.soundPlayed = true;
                return;
            }

            try {
                SoundManager.PlaySound();
                lastSoundNotificationTicks[viewerKey] = currentTick;
                viewer.soundPlayed = true;
            } catch { }
        }

        public HashSet<ViewerInfo> GetAllViewers() {
            var set = new HashSet<ViewerInfo>();

            foreach (var viewer in viewers.Values) {
                set.Add(viewer);
            }

            foreach (var stalkers in stalkerViewers.Values) {
                foreach (var viewer in stalkers.Values) {
                    set.Add(viewer);
                }
            }

            foreach (var viewer in unknownViewer.Values) {
                set.Add(viewer);
            }

            foreach (var key in stalkerLooksAt.Values) {
                if (key != null) {
                    set.Add(key);
                }
            }

            foreach (var stalk in objectStalks.Values) {
                foreach (var viewer in stalk.Viewers.Values) {
                    set.Add(viewer);
                }

                if (stalk.TargetPlayer != null) {
                    set.Add(stalk.TargetPlayer);
                }
            }

            return set;
        }

        public List<ViewerInfo> GetViewers() {
            var ordered = viewers.Values.OrderByDescending(v => v.IsActive).ThenByDescending(v => v.LastSeen);

            if (!Configuration.KeepHistory) {
                return ordered.Where(v => v.IsActive).ToList();
            }

            var active = ordered.Where(v => v.IsActive);
            var previous = ordered.Where(v => !v.IsActive && (Configuration.HistoryWhenClosed || !v.SeenWhileClosed)).Take(Math.Max(0, Configuration.NumHistory));
            
            return active.Concat(previous).ToList();
        }

        public void TargetCharacter(ViewerInfo viewer) {
            var character = FindCharacterInObjectTable(viewer);
            if (character != null) {
                TargetManager.Target = character;
            } else {
                ChatGui.PrintError($"[Peeping Cat] Cannot Target - {viewer.Name}.");
            }
        }

        public void HighlightCharacter(ViewerInfo viewer) {
            if (viewer.isFocused) {
                RestoreFocusTarget();
                viewer.isFocused = false;
                return;
            }

            var character = FindCharacterInObjectTable(viewer);
            if (character == null) {
                return;
            }

            if (!focusSaved) {
                savedFocusId = TargetManager.FocusTarget?.GameObjectId ?? 0;
                focusSaved = true;
            }

            TargetManager.FocusTarget = character;
            viewer.isFocused = true;
        }

        private void RestoreFocusTarget() {
            if (!focusSaved) {
                return;
            }

            TargetManager.FocusTarget = savedFocusId != 0 ? ObjectTable.SearchById(savedFocusId) : null;
            focusSaved = false;
            savedFocusId = 0;
        }

        public IPlayerCharacter? FindCharacterInObjectTable(ViewerInfo viewer) {
            foreach (var obj in ObjectTable) {
                if (obj is IPlayerCharacter character) {
                    if (character.Name.TextValue == viewer.Name && GetWorldName(character.HomeWorld.RowId) == viewer.World) {
                        return character;
                    }
                }
            }
            
            return null;
        }

        private Dictionary<string, ulong> FindLoadedViewersInObjectTable(HashSet<ViewerInfo> viewerList) {
            var loadedViewers = new Dictionary<string, ulong>();
            var objectTableKeys = new Dictionary<string, ulong>();

            foreach (var obj in ObjectTable) {
                if (obj is IPlayerCharacter character) {
                    string key = GetPlayerKey(character);
                    objectTableKeys[key] = character.GameObjectId;
                }
            }

            foreach (var viewer in viewerList) {
                string key = GetViewerKey(viewer);
                
                if (objectTableKeys.ContainsKey(key)) {
                    loadedViewers[key] = objectTableKeys[key];
                }
            }

            return loadedViewers;
        }

        public void OpenChatWith(ViewerInfo viewer, string message) {
            string command = $"/tell {viewer.Name}@{viewer.World} {message}";
            Svc.Framework.RunOnTick(() => {
                Chat.SendMessage(command);
            });
        }

        private sealed class SearchInfoRequest {
            public string Name = string.Empty;
            public string World = string.Empty;
            public ulong Cid;
            public DateTime Started;
            public DateTime LastSent = DateTime.MinValue;
        }

        private static readonly ushort[] SearchAllLocationIds = {
            0x01F4, 0x01F6, 0x01F5, 0x01FA, 0x01FB, 0x01F8, 0x01F9, 0x01F7, 0x0200, 0x01FC, 0x01FE, 0x1028, 0x01FF, 0x1029, 0x0202, 0x0201, 0x0203, 0x0EA1, 0x0E9D, 0x102A, 0x0EA0, 0x0E9C, 0x11A3, 0x119F, 0x11A0, 0x11A2, 0x0204, 0x0205, 0x0206, 0x01FD, 0x0E9E, 0x0E9F, 0x11A1, 0x0207
        };

        private SearchInfoRequest? searchInfoRequest;
        private const double SearchInfoTimeoutSeconds = 8.0;
        private const double SearchInfoResendSeconds = 3.0;
        
        private unsafe InfoProxyCommonList.CharacterData* searchInfoData;

        public unsafe void OpenSearchInfo(ViewerInfo viewer) {
            if (string.IsNullOrWhiteSpace(viewer.Name)) {
                return;
            }

            try {
                IPlayerCharacter? character = viewer.isLoaded ? Svc.Objects.SearchById(viewer.lastKnownGameObjectId) as IPlayerCharacter : null;
                ulong cid = viewer.cid != 0 ? viewer.cid : (character != null ? character.Struct()->ContentId : 0);

                if (cid != 0) {
                    if (searchInfoData == null) {
                        searchInfoData = (InfoProxyCommonList.CharacterData*) NativeMemory.AllocZeroed((nuint) sizeof(InfoProxyCommonList.CharacterData));
                    }

                    *searchInfoData = default;
                    searchInfoData->ContentId = cid;
                    searchInfoData->State = InfoProxyCommonList.CharacterData.OnlineStatus.Online;
                    searchInfoData->NameString = viewer.Name;

                    if (character != null) {
                        searchInfoData->CurrentWorld = (ushort) character.CurrentWorld.RowId;
                        searchInfoData->HomeWorld = (ushort) character.HomeWorld.RowId;
                        searchInfoData->Location = (ushort) ClientState.TerritoryType;
                        searchInfoData->Job = (byte) character.ClassJob.RowId;
                        searchInfoData->Sex = character.Customize.Length > 1 ? character.Customize[1] : (byte) 0;
                        searchInfoData->NameString = character.Name.TextValue;
                    } else {
                        ushort homeWorld = 0;
                        foreach (var kv in worldNames) {
                            if (string.Equals(kv.Value, viewer.World, StringComparison.OrdinalIgnoreCase)) {
                                homeWorld = (ushort) kv.Key;
                                break;
                            }
                        }
                        
                        searchInfoData->HomeWorld = homeWorld;
                        searchInfoData->CurrentWorld = homeWorld;
                    }

                    AgentDetail.Instance()->OpenForCharacterData(searchInfoData);
                    return;
                }
            } catch { }

            searchInfoRequest = new SearchInfoRequest {
                Name = viewer.Name,
                World = viewer.World,
                Cid = viewer.cid,
                Started = DateTime.UtcNow
            };
        }

        private unsafe void OnSearchInfoUpdate(IFramework framework) {
            var request = searchInfoRequest;
            if (request == null) {
                return;
            }

            try {
                var now = DateTime.UtcNow;
                if ((now - request.Started).TotalSeconds > SearchInfoTimeoutSeconds) {
                    searchInfoRequest = null;
                    ChatGui.PrintError($"[Peeping Cat] No Search Info Found - {request.Name}.");
                    return;
                }

                var module = InfoModule.Instance();
                if (module == null) {
                    return;
                }

                var proxy = (InfoProxySearch*) module->GetInfoProxyById(InfoProxyId.PlayerSearch);
                if (proxy == null) {
                    searchInfoRequest = null;
                    return;
                }

                var list = &proxy->InfoProxyCommonList;

                if (request.LastSent == DateTime.MinValue || (now - request.LastSent).TotalSeconds >= SearchInfoResendSeconds) {
                    proxy->NameString = request.Name;
                    proxy->JobMask = 0x7FFFFFFFFFFul;
                    proxy->LevelMin = 1;
                    proxy->LevelMax = 100;
                    proxy->GrandCompanyMask = 0xFF;
                    proxy->LanguageMask = 0xFF;
                    proxy->OnlineStatusMask = 1ul << 47;

                    ushort* locationIds = (ushort*) ((byte*) proxy + 0x100);
                    for (int i = 0; i < SearchAllLocationIds.Length; i++) {
                        locationIds[i] = SearchAllLocationIds[i];
                    }
                    proxy->LocationCount = (byte) SearchAllLocationIds.Length;

                    list->ClearData();
                    list->InfoProxyPageInterface.InfoProxyInterface.RequestData();
                    request.LastSent = now;
                    return;
                }

                uint count = list->InfoProxyPageInterface.InfoProxyInterface.EntryCount;
                if (count == 0 || list->CharData == null) {
                    return;
                }

                for (uint i = 0; i < count; i++) {
                    var entry = list->CharData + i;

                    bool match;
                    if (request.Cid != 0) {
                        match = entry->ContentId == request.Cid;
                    } else {
                        match = string.Equals(entry->NameString, request.Name, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(request.World) || string.Equals(GetWorldName(entry->HomeWorld), request.World, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!match) {
                        continue;
                    }

                    searchInfoRequest = null;
                    AgentDetail.Instance()->OpenForCharacterData(entry);
                    return;
                }
            } catch (Exception ex) {
                searchInfoRequest = null;
                ChatGui.PrintError($"[Peeping Cat] Search Info Failed: {ex.Message}");
            }
        }

        public bool HasXIVMessagerIpc() {
            return xivMessengerOpen.HasAction;
        }

        private bool TryOpenXIVMessager(ViewerInfo viewer) {
            if (!xivMessengerOpen.HasAction) {
                return false;
            }

            if (string.IsNullOrWhiteSpace(viewer.Name) || string.IsNullOrWhiteSpace(viewer.World) || viewer.World == "Unknown") {
                return false;
            }

            try {
                xivMessengerOpen.InvokeAction(GetViewerKey(viewer), true);
                return true;
            } catch (Exception ex) {
                Svc.Log.Warning($"XIV Instant Messenger IPC failed: {ex.Message}");
                return false;
            }
        }

        public void OpenMessageWindow(ViewerInfo viewer) {
            if (Configuration.UseXIVMessenger && TryOpenXIVMessager(viewer)) {
                return;
            }

            if (messageWindow == null) {
                messageWindow = new MessageWindow(this, viewer);
                WindowSystem.AddWindow(messageWindow);
            } else {
                messageWindow.Show(viewer);
            }
        }

        private static string CleanName(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return text;
            }

            text = text.Trim();
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        
        public bool HasPetNicknames() {
            try {
                return petNicknamesEnabled.HasFunction && petNicknamesEnabled.InvokeFunc();
            } catch {
                return false;
            }
        }

        private bool EnsurePetNicknameData() {
            if (petNames != null && petAddresses != null) {
                return true;
            }

            long now = Environment.TickCount64;
            if (now - lastPetLookupTick < 5000) {
                return false;
            }
            lastPetLookupTick = now;

            try {
                if (PluginInterface.TryGetData<List<string>>("PetRenamer.PetNicknamesStrings", out var names)) {
                    petDataTouched = true;

                    if (PluginInterface.TryGetData<List<nint>>("PetRenamer.PetNicknamesAddresses", out var addresses)) {
                        petNames = names;
                        petAddresses = addresses;
                        PluginInterface.TryGetData("PetRenamer.GameObjectRenameDict", out petLegacyNames);
                        PluginInterface.TryGetData("PetRenamer.PetNicknamesTextColours", out petTextColours);
                        PluginInterface.TryGetData("PetRenamer.PetNicknamesEdgeColours", out petEdgeColours);
                        return true;
                    }
                }
            } catch { }

            return false;
        }

        private bool TryGetPetNickname(IGameObject obj, out string nickname, out Vector4? textColour, out Vector4? edgeColour) {
            nickname = string.Empty;
            textColour = null;
            edgeColour = null;

            if (!Configuration.UsePetNicknames || !EnsurePetNicknameData()) {
                return false;
            }

            try {
                nint address = obj.Address;
                int count = Math.Min(petAddresses!.Count, petNames!.Count);

                for (int i = 0; i < count; i++) {
                    if (petAddresses[i] != address || string.IsNullOrWhiteSpace(petNames[i])) {
                        continue;
                    }

                    nickname = petNames[i];
                    textColour = ToColour(petTextColours, i);
                    edgeColour = ToColour(petEdgeColours, i);
                    return true;
                }

                if (petLegacyNames != null && petLegacyNames.TryGetValue(obj.GameObjectId, out var legacy) && !string.IsNullOrWhiteSpace(legacy)) {
                    nickname = legacy;
                    return true;
                }
            } catch { }

            return false;
        }

        private static Vector4? ToColour(List<Vector3?>? list, int index) {
            if (list == null || index >= list.Count) {
                return null;
            }

            var colour = list[index];
            return colour.HasValue ? new Vector4(colour.Value.X, colour.Value.Y, colour.Value.Z, 1f) : null;
        }
        
        public bool IsMainWindowOpen() {
            return MainWindow.IsOpen;
        }

        public string GetWorldName(uint rowId) {
            if (worldNames.TryGetValue(rowId, out var worldName)) {
                return worldName;
            }
            
            return "Unknown";
        }

        public string GetViewerKey(ViewerInfo viewer) {
            return $"{viewer.Name}@{viewer.World}";
        }

        public string GetPlayerKey(IPlayerCharacter character) {
            string world = GetWorldName(character.HomeWorld.RowId);
            return $"{character.Name.TextValue}@{world}";
        }

        public class ViewerInfo {
            public string Name { get; set; } = string.Empty;
            public string World { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool isLoaded { get; set; }
            public bool isFocused { get; set; }
            public bool soundPlayed { get; set; }
            public DateTime FirstSeen { get; set; }
            public DateTime LastSeen { get; set; }
            public ulong lastKnownGameObjectId { get; set; }
            public ulong cid { get; set; }
            public bool SeenWhileClosed { get; set; }
        }
        
        public class ObjectTargetInfo {
            public string Name { get; set; } = string.Empty;
            public string KindLabel { get; set; } = string.Empty;
            public ulong GameObjectId { get; set; }
            public string OriginalName { get; set; } = string.Empty;
            public string OwnerName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public Vector4? TextColour { get; set; }
            public Vector4? EdgeColour { get; set; }
        }
        
        public class ObjectStalk {
            public ObjectTargetInfo Info { get; set; } = new();
            public Dictionary<string, ViewerInfo> Viewers { get; } = new();
            public ViewerInfo? TargetPlayer { get; set; }
            public ObjectTargetInfo? TargetObject { get; set; }
            public bool IsLoaded { get; set; }
            public long LastRebind { get; set; }
            public StalkerWindow? Window { get; set; }
        }

        public static string GetObjectKey(ObjectTargetInfo info) {
            return $"{info.KindLabel}|{info.OriginalName}|{info.OwnerName}";
        }

        private const long OwnCompanionScanIntervalMs = 500;
        private ulong ownMinionId;
        private ulong ownPetId;
        private long lastOwnCompanionScan;

        private unsafe void ReadNameplate(IGameObject obj, out string title, out Vector4? textColour, out Vector4? edgeColour) {
            title = string.Empty;
            textColour = null;
            edgeColour = null;

            try {
                var module = RaptureAtkModule.Instance();
                var addon = Svc.GameGui.GetAddonByName<AddonNamePlate>("NamePlate");

                if (module != null && addon != null) {
                    int count = Math.Min(module->NameplateInfoCount, AddonNamePlate.NumNamePlateObjects);
                    var entries = module->NamePlateInfoEntries;

                    for (int i = 0; i < count; i++) {
                        if (entries[i].ObjectId.Id != obj.GameObjectId) {
                            continue;
                        }

                        title = entries[i].Title.ToString().Trim();

                        var plate = addon->NamePlateObjectArray + i;
                        if (plate->NameText != null) {
                            var text = plate->NameText->TextColor;
                            var edge = plate->NameText->EdgeColor;

                            if (text.A != 0) {
                                textColour = new Vector4(text.R / 255f, text.G / 255f, text.B / 255f, 1f);
                            }

                            if (edge.A != 0) {
                                edgeColour = new Vector4(edge.R / 255f, edge.G / 255f, edge.B / 255f, 1f);
                            }
                        }

                        break;
                    }
                }
            } catch { }

            if (title.Length == 0 && obj.ObjectKind == ObjectKind.EventNpc) {
                try {
                    if (Svc.Data.GetExcelSheet<ENpcResident>().TryGetRow(obj.BaseId, out var resident)) {
                        title = resident.Title.ExtractText().Trim();
                    }
                } catch { }
            }
        }
        
        private unsafe void RefreshOwnCompanions(bool force = false) {
            long now = Environment.TickCount64;
            if (!force && now - lastOwnCompanionScan < OwnCompanionScanIntervalMs) {
                return;
            }

            lastOwnCompanionScan = now;
            ownMinionId = 0;
            ownPetId = 0;

            var player = ObjectTable.LocalPlayer;
            if (player == null) {
                return;
            }

            foreach (var obj in ObjectTable) {
                if (obj.ObjectKind == ObjectKind.Companion) {
                    var companion = (Companion*) obj.Address;
                    if ((nint) companion->Owner == player.Address || (obj.OwnerId != 0 && obj.OwnerId == player.EntityId)) {
                        ownMinionId = obj.GameObjectId;
                    }
                } else if (obj.ObjectKind == ObjectKind.BattleNpc && obj.SubKind == 2 && obj.OwnerId == player.EntityId) {
                    ownPetId = obj.GameObjectId;
                }
            }
        }

        public bool HasOwnMinion() {
            RefreshOwnCompanions();
            return ownMinionId != 0;
        }

        public bool HasOwnPet() {
            RefreshOwnCompanions();
            return ownPetId != 0;
        }

        public void OpenOwnMinionStalkWindow() {
            RefreshOwnCompanions(true);
            var obj = ownMinionId != 0 ? ObjectTable.SearchById(ownMinionId) : null;
            if (obj != null) {
                OpenObjectStalkWindow(BuildObjectInfo(obj));
            }
        }

        public void OpenOwnPetStalkWindow() {
            RefreshOwnCompanions(true);
            var obj = ownPetId != 0 ? ObjectTable.SearchById(ownPetId) : null;
            if (obj != null) {
                OpenObjectStalkWindow(BuildObjectInfo(obj));
            }
        }

        public void OpenObjectStalkWindow(ObjectTargetInfo info) {
            string key = GetObjectKey(info);

            if (objectStalks.TryGetValue(key, out var existing)) {
                if (existing.Window != null) {
                    existing.Window.IsOpen = true;
                }

                return;
            }

            var stalk = new ObjectStalk {
                Info = new ObjectTargetInfo {
                    Name = info.Name,
                    OriginalName = info.OriginalName,
                    OwnerName = info.OwnerName,
                    KindLabel = info.KindLabel,
                    GameObjectId = info.GameObjectId,
                    Title = info.Title,
                    TextColour = info.TextColour,
                    EdgeColour = info.EdgeColour
                },
                IsLoaded = true
            };

            var window = new StalkerWindow(this, key, stalk.Info.Name);
            stalk.Window = window;
            objectStalks[key] = stalk;
            WindowSystem.AddWindow(window);
        }

        public void CloseObjectStalkWindow(string key) {
            if (objectStalks.Remove(key, out var stalk) && stalk.Window != null) {
                WindowSystem.RemoveWindow(stalk.Window);
            }
        }

        public ObjectStalk? GetObjectStalk(string key) {
            return objectStalks.TryGetValue(key, out var stalk) ? stalk : null;
        }

        public List<ViewerInfo> GetObjectStalkViewers(string key) {
            if (!objectStalks.TryGetValue(key, out var stalk)) {
                return new List<ViewerInfo>();
            }

            var ordered = stalk.Viewers.Values.OrderByDescending(v => v.IsActive).ThenByDescending(v => v.LastSeen);

            if (!Configuration.KeepHistory) {
                return ordered.Where(v => v.IsActive).ToList();
            }

            var active = ordered.Where(v => v.IsActive);
            var previous = ordered.Where(v => !v.IsActive).Take(Math.Max(0, Configuration.NumHistory));

            return active.Concat(previous).ToList();
        }

        private ObjectTargetInfo BuildObjectInfo(IGameObject obj) {
            string kindLabel = DescribeTarget(obj);
            string originalName = CleanName(obj.Name.TextValue);
            string name = originalName;

            ReadNameplate(obj, out string title, out Vector4? textColour, out Vector4? edgeColour);

            if (nameplateCache.TryGetValue(obj.GameObjectId, out var cached)) {
                if (title.Length == 0) {
                    title = cached.Title;
                }

                textColour ??= cached.Text;
                edgeColour ??= cached.Edge;
            }

            if (title.Length > 0 || textColour.HasValue || edgeColour.HasValue) {
                if (nameplateCache.Count > 512) {
                    nameplateCache.Clear();
                }

                nameplateCache[obj.GameObjectId] = (title, textColour, edgeColour);
            }

            if (kindLabel is "Pet" or "Minion") {
                title = string.Empty;
            }

            if (TryGetPetNickname(obj, out var nickname, out var nickText, out var nickEdge)) {
                name = nickname;
                textColour = nickText ?? textColour;
                edgeColour = nickEdge ?? edgeColour;
            }

            if (string.IsNullOrWhiteSpace(name)) {
                name = $"Unnamed {kindLabel}";
            }

            return new ObjectTargetInfo {
                Name = name,
                OriginalName = originalName,
                OwnerName = GetOwner(obj),
                KindLabel = kindLabel,
                GameObjectId = obj.GameObjectId,
                Title = title,
                TextColour = textColour,
                EdgeColour = edgeColour
            };
        }

        private IGameObject? FindObjectByIdentity(ObjectTargetInfo info) {
            foreach (var obj in ObjectTable) {
                if (obj is IPlayerCharacter) {
                    continue;
                }

                if (CleanName(obj.Name.TextValue) != info.OriginalName || GetOwner(obj) != info.OwnerName) {
                    continue;
                }

                return obj;
            }

            return null;
        }

        private void UpdateObjectStalks(long currentTick) {
            if (objectStalks.Count == 0) {
                return;
            }

            var idToKey = new Dictionary<ulong, string>();
            var lookingAt = new Dictionary<string, HashSet<string>>();

            foreach (var entry in objectStalks) {
                var stalk = entry.Value;
                IGameObject? obj = ObjectTable.SearchById(stalk.Info.GameObjectId);

                if (obj == null || obj is IPlayerCharacter || CleanName(obj.Name.TextValue) != stalk.Info.OriginalName) {
                    obj = null;

                    if (currentTick - stalk.LastRebind >= LoadingIntervalMs) {
                        stalk.LastRebind = currentTick;
                        obj = FindObjectByIdentity(stalk.Info);
                    }
                }

                if (obj == null) {
                    stalk.IsLoaded = false;
                    stalk.TargetPlayer = null;
                    stalk.TargetObject = null;

                    foreach (var viewer in stalk.Viewers.Values) {
                        viewer.IsActive = false;
                    }

                    continue;
                }

                stalk.IsLoaded = true;
                stalk.Info.GameObjectId = obj.GameObjectId;
                idToKey[obj.GameObjectId] = entry.Key;
                lookingAt[entry.Key] = new HashSet<string>();

                UpdateObjectOwnTarget(stalk, obj);
            }

            if (idToKey.Count == 0) {
                return;
            }

            foreach (var obj in ObjectTable) {
                if (obj is not IPlayerCharacter character || !idToKey.TryGetValue(character.TargetObjectId, out var key)) {
                    continue;
                }

                string charKey = GetPlayerKey(character);
                lookingAt[key].Add(charKey);

                var viewers = objectStalks[key].Viewers;
                if (!viewers.TryGetValue(charKey, out var viewer)) {
                    viewers[charKey] = CreateViewer(character);
                } else {
                    viewer.IsActive = true;
                    viewer.LastSeen = DateTime.Now;
                }
            }

            foreach (var entry in lookingAt) {
                foreach (var viewer in objectStalks[entry.Key].Viewers) {
                    if (!entry.Value.Contains(viewer.Key)) {
                        viewer.Value.IsActive = false;
                    }
                }
            }
        }

        private void UpdateObjectOwnTarget(ObjectStalk stalk, IGameObject obj) {
            ulong targetId = GetLookTargetId(obj);
            var target = targetId == 0 || targetId == 0xE0000000 ? null : ObjectTable.SearchById(targetId);

            if (target == null) {
                stalk.TargetPlayer = null;
                stalk.TargetObject = null;
            } else if (target is IPlayerCharacter player) {
                stalk.TargetObject = null;

                if (stalk.TargetPlayer == null || GetViewerKey(stalk.TargetPlayer) != GetPlayerKey(player)) {
                    stalk.TargetPlayer = CreateViewer(player);
                }
            } else {
                stalk.TargetPlayer = null;
                stalk.TargetObject = BuildObjectInfo(target);
            }
        }

        public ObjectTargetInfo? GetStalkerObjectTarget(ViewerInfo stalker) {
            return stalkerObjectTarget.TryGetValue(GetViewerKey(stalker), out var target) ? target : null;
        }

        public void TargetObject(ObjectTargetInfo info) {
            var obj = ObjectTable.SearchById(info.GameObjectId);
            if (obj != null) {
                TargetManager.Target = obj;
            } else {
                ChatGui.PrintError($"[Peeping Cat] Cannot Target - {info.Name}.");
            }
        }
        
        public void OpenObjectContextMenu(ObjectTargetInfo info) {
            var obj = ObjectTable.SearchById(info.GameObjectId);
            if (obj == null) {
                ChatGui.PrintError($"[Peeping Cat] Cannot Open Context Menu - {info.Name}.");
                return;
            }

            unsafe {
                Svc.Framework.RunOnTick(() => {
                    AgentHUD.Instance()->OpenContextMenuFromTarget(obj.Struct());
                });
            }
        }

        private unsafe string GetOwner(IGameObject obj) {
            try {
                if (obj.ObjectKind.ToString() == "Companion") {
                    var companion = (Companion*) obj.Address;
                    nint ownerAddress = (nint) companion->Owner;

                    if (ownerAddress != 0) {
                        foreach (var other in ObjectTable) {
                            if (other is IPlayerCharacter player && player.Address == ownerAddress) {
                                return player.Name.TextValue;
                            }
                        }
                    }
                }

                if (obj.OwnerId != 0 && obj.OwnerId != 0xE0000000) {
                    if (ObjectTable.SearchByEntityId(obj.OwnerId) is IPlayerCharacter ownerCharacter) {
                        return ownerCharacter.Name.TextValue;
                    }
                }
            } catch { }

            return string.Empty;
        }
        
        private unsafe ulong GetLookTargetId(IGameObject obj) {
            if (obj is not ICharacter) {
                return obj.TargetObjectId;
            }

            var chara = (Character*)obj.Address;
            var controller = &chara->LookAt.Controller;
            int count = Math.Min(controller->ParamCount, 6);

            foreach (int i in new[] { 1, 2, 0 }) {
                if (i >= count) {
                    continue;
                }

                var param = controller->Params[i].TargetParam;
                if (param.Type == CharacterLookAtTargetParam.TargetInfoType.GameObjectId
                    && param.TargetId.Id != 0 && param.TargetId.Id != 0xE0000000) {
                    return param.TargetId.Id;
                }
            }

            return obj.TargetObjectId;
        }
        
        public static bool IsShownTargetKind(string kindLabel) {
            return kindLabel is "Player" or "Pet" or "Minion";
        }

        private static string DescribeTarget(IGameObject obj) {
            string kind = obj.ObjectKind.ToString();

            if (kind == "BattleNpc") {
                return obj.SubKind switch {
                    1 => "Spot",
                    2 => "Pet",
                    3 => "Chocobo",
                    5 => "Enemy",
                    6 => "Race",
                    7 => "Verminion",
                    9 => "Trust",
                    _ => "Battle"
                };
            }

            return kind switch {
                "Pc" => "Player",
                "EventNpc" => "NPC",
                "Treasure" => "Box",
                "Aetheryte" => "Aether",
                "GatheringPoint" => "Node",
                "EventObj" => "Object",
                "Mount" => "Mount",
                "Companion" => "Minion",
                "Retainer" => "Retainer",
                "AreaObject" => "Area",
                "HousingEventObject" => "Housing",
                "Cutscene" => "Cutscene",
                "ReactionEventObject" => "Reaction",
                "Ornament" => "Accessory",
                "CardStand" => "Card",
                "FollowMount" => "Mount",
                _ => kind
            };
        }

        public ViewerInfo CreateViewer(IPlayerCharacter character) {
            ulong cid;
            unsafe {
                cid = character.Struct()->ContentId;
            }

            return new ViewerInfo {
                Name = character.Name.TextValue,
                World = GetWorldName(character.HomeWorld.RowId),
                IsActive = true,
                isLoaded = true,
                isFocused = false,
                soundPlayed = false,
                FirstSeen = DateTime.Now,
                LastSeen = DateTime.Now,
                lastKnownGameObjectId = character.GameObjectId,
                cid = cid
            };
        }
    }
}
