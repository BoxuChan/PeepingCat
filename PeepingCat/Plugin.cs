using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
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
using Lumina.Excel.Sheets;

using PeepingCat.Windows;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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

        private const string CommandName1 = "/pcat";
        private const string CommandName2 = "/peepingcat";
        private const string CommandName3 = "/pcatconfig";

        private Dictionary<string, ViewerInfo> viewers = new();
        private Dictionary<string, ViewerInfo> unknownViewer = new();
        private Dictionary<string, Dictionary<string, ViewerInfo>> stalkerViewers = new();
        private Dictionary<string, StalkerWindow> stalkerWindows = new();
        private Dictionary<string, ViewerInfo?> stalkerLooksAt = new();
        private MessageWindow? messageWindow;

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("Peeping Cat");
        private MainWindow MainWindow { get; init; }
        private ConfigWindow ConfigWindow { get; init; }
        private Helpers.SoundManager SoundManager { get; init; }

        private readonly Dictionary<uint, string> worldNames = new();

        private long lastUpdateTick;
        private const int UpdateIntervalMs = 100;

        private long lastLoadingTick;
        private const int LoadingIntervalMs = 1500;

        private bool firstDrawn;
        private bool dutyWindowSuppressed;
        private readonly Dictionary<string, long> lastSoundNotificationTicks = new();

        private readonly ICallGateSubscriber<IPlayerCharacter, List<MoodlesStatusInfo>> GetStatusManagerInfoByPlayerV2;

        public Plugin() {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            MainWindow = new MainWindow(this);
            ConfigWindow = new ConfigWindow(this);
            SoundManager = new Helpers.SoundManager(this);

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(ConfigWindow);

            CommandManager.AddHandler(CommandName1, new CommandInfo(OnCommand) {
                HelpMessage = "Opens P-Cat Window."
            });

            CommandManager.AddHandler(CommandName2, new CommandInfo(OnCommand) {
                HelpMessage = "Opens P-Cat Window."
            });

            CommandManager.AddHandler(CommandName3, new CommandInfo(OnConfig) {
                HelpMessage = "Opens P-Cat Config."
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
        }

        private bool IsInDutyInstance() {
            try {
                return Condition[ConditionFlag.BoundByDuty];
            } catch {
                return false;
            }
        }

        public bool ShouldSuppressDutySound() {
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

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfig;
            PluginInterface.UiBuilder.OpenMainUi -= DrawMain;

            Framework.Update -= OnUpdate;
            Framework.Update -= OnSearchInfoUpdate;
            
            unsafe {
                if (searchInfoData != null) {
                    NativeMemory.Free(searchInfoData);
                    searchInfoData = null;
                }
            }

            ECommonsMain.Dispose();
        }

        private void OnCommand(string command, string args) {
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

        private void DrawUI() => WindowSystem.Draw();
        public void DrawConfig() => OnConfig("/pcatconfig", "");
        private void DrawMain() => OnCommand("/pcat", "");

        public bool HasMoodlesIpc() {
            return GetStatusManagerInfoByPlayerV2.HasFunction;
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

            foreach (var obj in ObjectTable) {
                if (obj is IPlayerCharacter character) {
                    bool isSelf = character.GameObjectId == localPlayerId;
                    bool targetsMe;

                    if (isSelf) {
                        targetsMe = Configuration.DetectSelfTarget && (character.TargetObjectId == localPlayerId || TargetManager.Target?.GameObjectId == localPlayerId);
                    } else {
                        targetsMe = character.TargetObjectId == localPlayerId;
                    }

                    if (targetsMe) {
                        string charKey = GetPlayerKey(character);
                        currentlyLookingAtMe.Add(charKey);

                        if (!viewers.TryGetValue(charKey, out var viewer)) {
                            viewer = CreateViewer(character);
                            viewers[charKey] = viewer;
                        } else {
                            viewer.IsActive = true;
                            viewer.LastSeen = DateTime.Now;
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
                        if (character.TargetObject == null) {
                            stalkerLooksAt[stalker2] = null;
                        } else {
                            var possibleTarget = ObjectTable.SearchById(character.TargetObjectId) as IPlayerCharacter;
                            if (possibleTarget == null) {
                                stalkerLooksAt[stalker2] = null;
                            } else {
                                if (stalkerLooksAt[stalker2] != null) {
                                    if (GetViewerKey(stalkerLooksAt[stalker2]!) != $"{possibleTarget.Name.TextValue}@{GetWorldName(possibleTarget.HomeWorld.RowId)}") {
                                        stalkerLooksAt[stalker2] = CreateViewer(possibleTarget);
                                    }
                                } else {
                                    stalkerLooksAt[stalker2] = CreateViewer(possibleTarget);
                                }
                            }
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

            return set;
        }

        public List<ViewerInfo> GetViewers() {
            return viewers.Values.OrderByDescending(v => v.IsActive).ThenByDescending(v => v.LastSeen).Take(15).ToList();
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
            var character = FindCharacterInObjectTable(viewer);
            if (character != null) {
                if (viewer.isFocused) {
                    TargetManager.FocusTarget = null;
                } else {
                    TargetManager.FocusTarget = character;
                }
                
                viewer.isFocused = !viewer.isFocused;
            }
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

        public void OpenMessageWindow(ViewerInfo viewer) {
            if (messageWindow == null) {
                messageWindow = new MessageWindow(this, viewer);
                WindowSystem.AddWindow(messageWindow);
            } else {
                messageWindow.Show(viewer);
            }
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
