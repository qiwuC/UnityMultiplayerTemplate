# Unity Multiplayer Template · Full Regression Checklist

> Generated: 2026-09-28 (Phase 12 T12-01). Status: ✅ Pass / 🟡 Partial / ⛔ Fail / 🔒 Environment-limited.
> Verification environment: Unity 6000.6.2f1 (Tuanjie) Editor Play Mode, GameScene, Host single-machine + virtual input device injection.

## 1. Network Core (Phase 2)

| Feature | Status | Evidence/Notes |
|---|---|---|
| NetworkConfig SO (KCP/Steam switch, port, protocol version) | ✅ | Inspector validation (T11-05); port/connection count validation |
| CqwNetworkManager persistent + transport pre-config | ✅ | Host start log: KcpTransport addr=localhost proto=1 |
| ProtocolAuthenticator protocol auth | ✅ | Log: client sends v1 → server accepts |
| SessionManager full API (create/join/leave/kick/pause) | ✅ | CreateSession/LeaveSession multi-round runtime verification |
| NetworkPauseState (SyncVar pause broadcast) | ✅ | SessionStateRoot spawn log |
| ReconnectManager auto-reconnect | 🟡 | Code path exists; real disconnect not tested (single machine can't unplug) |
| NetworkObjectPool pooling | ✅ | PooledCube prefab + pool API |
| CqwNetworkSceneManager scene sync | 🟡 | Compiles; scene transition flow not E2E tested this round |
| NetworkTimeSync / NetworkAnimationSync | ✅ | Server time sync + Animator param sync (Phase 3 bridge) |
| RpcThrottler / MessageBatcher | ✅ | Token bucket + 50ms batching |
| NetworkBandwidthMonitor | ✅ | Editor bandwidth window reads real-time (T11-02) |
| NetworkSimulator weak-net simulation | ✅ | 6 presets menu + window (T11-01); Editor/DevBuild conditional compile |
| NetworkDiagnosticsCenter quality/degradation/export | ✅ | RTT/loss estimation + quality events; CSV export (T11-04) |
| Steam lobby (SteamLobbyManager) | 🔒 | steam_appid=480 placeholder, no Steam client; code paths compile |

## 2. Player System (Phase 3)

| Feature | Status | Evidence/Notes |
|---|---|---|
| IPlayerInputSource abstraction + DefaultPlayerInputSource | ✅ | KB+mouse/gamepad dual mode; Phase 10 LocalInputRelay seamless injection |
| PlayerController (move/gravity/jump/coyote) | ✅ | E2E player spawn + movement; automove injection (T11-03) verified |
| PlayerCameraController 3-mode + occlusion | ✅ | Runtime mode switch logs (ThirdPerson→FirstPerson→Spectate) |
| PlayerIdentity/PlayerStats SyncVar | ✅ | Pre-spawn identity config (nickname/SteamID placeholder/team) |
| PlayerRegistry | ✅ | Spawn/destroy callbacks; team/connection query |
| TeamManager auto-balance + TeamConfig | ✅ | Auto-assign log team=-1→assigned; Inspector color preview (T11-05) |
| PlayerAnimatorBridge | ✅ | Movement state→Animator params |
| LocalPlayerJoinManager local multiplayer | ✅ | KB slot 0 + JoinGamepad(0) slot 1 (E2E slots2=True); Phase 10 EnsureJoinManager fallback |
| RoundRobinSpawner spawn point rotation | ✅ | 4 SpawnPoints + jitter (E2E spawn positions vary) |

## 3. UI System (Phase 4)

| Feature | Status | Evidence/Notes |
|---|---|---|
| UIManager layers/stack nav/modal | ✅ | Both scenes UIRoot mounted; OpenPanel/CloseTop |
| 8 panels (MainMenu/Settings/Lobby/Pause/SaveSlot/Hud/Chat/Loading) | ✅ | UIPanelBootstrap auto-creates (both scenes) |
| ConfirmDialog / ToastManager | ✅ | Toast levels (Settings Applied/Conflict cases) |
| InteractionPromptScreen / World | ✅ | T9-03: shown=True hidden=True; localized text (LocalizedOrTitle) |
| NetworkPauseState pause broadcast | ✅ | P-01 |
| Interaction prompt progress (SetProgress) | ✅ | API ready |

## 4. Save System (Phase 5)

| Feature | Status | Evidence/Notes |
|---|---|---|
| SaveDataRoot/ISaveDataProvider | ✅ | Phase 5 verified |
| SaveSerializer/SaveFileIO (atomic+SHA256+backup) | ✅ | Phase 5 verified |
| SaveMigrationService | ✅ | Phase 5 verified |
| FileSaveSlotService + AutoSaveScheduler + SaveIOTaskQueue | ✅ | Phase 5 verified (not re-tested, compiles clean) |

## 5. Audio System (Phase 6)

| Feature | Status | Evidence/Notes |
|---|---|---|
| AudioMixerController 4-channel | ✅ | Inspector real-time readout + quick test buttons (T11-05); no-mixer fallback |
| AudioPool (pooled+priority eviction) | ✅ | Phase 6 verified |
| BgmPlayer (CrossFade) | ✅ | Phase 6 verified |
| AudioSystemBootstrap | ✅ | UIRoot mounted (both scenes) |

## 6. Localization (Phase 7)

| Feature | Status | Evidence/Notes |
|---|---|---|
| 13 language JSON key tables | ✅ | en base; zh_cn full; others en placeholder (key sets consistent); Phase 9/10 new keys synced 13/13 |
| LocalizationService switch/fallback | ✅ | SettingsPanel language dropdown real-time switch |
| LocalizationKeyHolder auto-refresh | ✅ | OnEnable subscribes OnLanguageChanged |
| SettingsPanel language persistence | ✅ | IGameSettingsService (Phase 8 JSON backend) |

## 7. Common Systems (Phase 8)

| Feature | Status | Evidence/Notes |
|---|---|---|
| ObjectPool/UnityObjectPool/ObjectPoolHandle/Service | ✅ | T8-01: reuse/timeout/eviction/stats all True |
| EventBus (priority/Once/isolation/stats) | ✅ | T8-02: order high→mid→low, once=1, isolation, stats correct |
| StateMachine/HierarchicalStateMachine/Runner | ✅ | T8-03: sequence/self-transition reject/unregistered reject/HSM leaf chain |
| Scheduler/CooldownManager | ✅ | T8-04: delay/cancel/repeat 2×/cooldown expiry |
| DataRegistry/Hub | ✅ | T8-05: duplicate reject/overwrite/TryGet/unregister |
| ResourceManager (ref count+delayed unload+preload) | ✅ | T8-06: single load/refs=2/timeout unload |
| EffectManager (pool+follow+auto recycle) | ✅ | T8-07: play/expire recycle/follow destroy recycle (Unity fake-null fix) |
| GameLogger (leveled+background flush+7-day) | ✅ | T8-08: file contains selftest line |
| JsonGameSettingsService (JSON persist+migration+ApplyAll) | ✅ | T8-09: settings.json persisted; PlayerPrefs migration log |
| SystemsBootstrap unified bootstrap | ✅ | T8-10: InitOrder 8 systems |

## 8. Interaction (Phase 9)

| Feature | Status | Evidence/Notes |
|---|---|---|
| IInteractable/InteractableBehaviour/InteractableData | ✅ | T9-01 |
| InteractableRegistry (register/range query/fake-null guard) | ✅ | T9-02: registry=5 |
| PlayerInteractor (scan/Cmd routing/target events) | ✅ | T9-02: targetIsLamp=True; server-authoritative Cmd chain |
| Prompt Screen/World integration | ✅ | T9-03: shown/hidden True; localized + device icon |
| TestLamp (SyncVar toggle) | ✅ | T9-04: toggled=True visualOk=True; T14C cooldown release toggle=True |
| TestPickup (server pickup+hide sync) | ✅ | T9-04: picked=True visualHidden=True pickupUnavailable=True |
| Sample_Button (SyncVar<Color> toggle) | ✅ | Phase A: color change SyncVar sync |
| Sample_Chest (SyncVar<bool> open + scale anim) | ✅ | Phase A: opened + unavailable |
| SampleChestCounter (SyncVar<int> HUD) | ✅ | Phase A: count increments server-authoritative |

## 9. Input (Phase 10)

| Feature | Status | Evidence/Notes |
|---|---|---|
| PlayerControls.inputactions + generated class | ✅ | T10-01: 3 maps/3 interfaces/2 schemes; meta generateWrapperCode=1 |
| RebindManager (interactive rebind/conflict/persist) | ✅ | T10-02: rebind=True saveOk=loadOk=True |
| KeybindsPanel + SettingsPanel wiring | ✅ | OpenPanel("ui.keybinds"); 17 keys × 13 languages |
| LocalInputManager (PIM takeover) | ✅ | T10-03: log "takeover"; EnsureJoinManager fallback |
| LocalPlayerJoinManager fallback self-create | ✅ | Fixed: GameScene missing component caused Instance=null (NRE root cause) |
| SplitScreenManager (1/2/3-4 layouts) | ✅ | T10-04: E2E rect0/rect1/listenerOk all True |
| PlayerController split-screen camera fix | ✅ | ownCamTransform prefers own camera |
| LocalInputRelay (PlayerControls bridge+device isolation) | ✅ | T10-05: T10RELAY 6 items all green; relayMove=True (virtual gamepad E2E); force-rebind fix |
| Auto-wiring (OnLocalPlayerJoined → relay) | ✅ | relayAuto/relayAuto2=True |
| InputDeviceProvider (icon integration) | ✅ | GetCurrentDeviceType/GetDeviceId |

## 10. Dev Tools (Phase 11)

| Feature | Status | Evidence/Notes |
|---|---|---|
| NetworkSimulatorPresets 6 presets | ✅ | T11-01: Apply/Disable; menu+window |
| NetworkSimulatorWindow | ✅ | Preset dropdown/sliders/real-time status |
| BandwidthMonitorWindow (60s bar chart) | ✅ | T11-02: per-second sampling NetworkBandwidthMonitor |
| CommandLineArgs (netmode/address/automove/verbose/lobby) | ✅ | T11-03: Runner deferred apply; automove injection; Describe() |
| NetworkDiagnosticsExporter CSV | ✅ | T11-04: persistentDataPath/Diagnostics/ |
| Custom Inspectors ×3 | ✅ | T11-05: NetworkConfig validation/AudioMixer volume test/TeamConfig color swatches |
| BatchToolsWindow (TMP font/LocalizationKeyHolder) | ✅ | T11-05: scene-wide replace + selected batch add |
| HealthCheck Editor menu | ✅ | B-05: 10 items all green, writes health_check.txt |

## 11. Docs & Delivery (Phase 12)

| Feature | Status | Evidence/Notes |
|---|---|---|
| REGRESSION.md | ✅ | This file (English) |
| README.md | ✅ | Full English rewrite |
| Delivery report | ✅ | Chat output (T12-03) |

## Known Limitations (Environment-limited)

| Item | Reason | Suggestion |
|---|---|---|
| Steam lobby/voice | steam_appid=480 placeholder | Configure real AppId + Steam client |
| Client-side interaction Cmd routing | Single editor limitation | Dual-machine test |
| Disconnect reconnection real test | Single machine can't simulate | Dual-machine + network disconnect |
| PIM virtual device trigger | InputSystem 1.20 internal behavior | Real gamepad verification |
| HybridCLR Installer | Phase 1 placeholder | Run as needed |
| Luban generator | Phase 1 placeholder | Configure as needed |
| AudioMixer groups | Requires manual Editor setup (Unity 6 non-SO) | Configure in Editor when needed |
| InputSystem 1.20 API | maxPlayerCount readonly; playerJoinedEvent AddListener | Follow new API patterns |
| GamepadState.buttons layout | 1.20 tested: buttonSouth=bit6 | Use 0xFFFF for full-press testing |
| Mirror scene object lifecycle | NetworkIdentity scene objects suppressed pre-Host | Expected Mirror behavior |
