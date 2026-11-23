# Unity Multiplayer Template

A 3D Low-Poly stylized multiplayer game framework built on Unity, targeting PC (Steam distribution).

## Project Info

| Item | Value |
|---|---|
| Product Name | Unity Multiplayer Template |
| Company | cqw |
| Package ID | com.cqw.multilayerframework |
| Unity Version | 6000.6.2f1 |
| Render Pipeline | URP (com.unity.render-pipelines.universal 17.6.0, PC_RPAsset) |
| Target Platform | Windows Standalone 64-bit (Steam) |
| Scripting Backend | IL2CPP |
| API Compatibility | .NET Framework (NET_Unity_4_8 / NET_4_6, required by Steamworks.NET) |
| Dynamic Batching | Disabled (URP asset m_SupportsDynamicBatching = 0) |
| Namespace | Cqw.MultilayerFramework |

## Directory Structure

```
Assets/
├─ Scripts/            # All C# scripts (namespace Cqw.MultilayerFramework)
│  ├─ Network/         # Mirror networking
│  ├─ Player/          # Player controls
│  ├─ UI/              # UI logic (all TextMeshPro)
│  ├─ Save/            # Save system
│  ├─ Audio/           # Audio
│  ├─ Localization/    # Localization (13 language keys)
│  ├─ Systems/         # Common systems (Phase 8)
│  ├─ Interaction/     # Interaction system (Phase 9)
│  ├─ Input/           # Input (Phase 10)
│  ├─ Tools/           # Dev tools (Phase 11)
│  └─ Editor/          # Editor scripts (Phase 11)
├─ Scenes/             # MainMenu, GameScene, SampleScene
├─ Resources/
├─ Settings/           # URP config + Input (PlayerControls.inputactions)
├─ Prefabs/
├─ ScriptableObjects/  # System configs
├─ Icons/              # App placeholder icons
└─ Codely/Fonts/       # TMP font assets
```

## Plugins

| Plugin | Purpose | Version | Import Method | Status |
|---|---|---|---|---|
| Mirror | Networking framework | 96.11.3 | GitHub Release unitypackage | ✅ |
| Steamworks.NET | Steam SDK C# bindings | 2025.164.1 | GitHub Release unitypackage (SHA256) | ✅ |
| FizzySteamworks | Steam transport for Mirror | 6.0.1 | UPM Git URL | ✅ |
| MetaVoiceChat | In-game voice chat | v4.3 | GitHub Release unitypackage (SHA256) | ✅ |
| HybridCLR | C# hot-update | 8.14.1 | gitee UPM Git URL | ✅ (Installer pending) |
| Luban | Config table toolchain | main (2026-09-10) | LubanLib runtime + dotnet generator | ✅ (generator pending) |
| TextMeshPro | UI text | 1.4.0 (com.unity.ugui 2.6.0) | Unity 6 built-in | ✅ |
| Input System | New input system | 1.20 (Unity 6) | Package Manager | ✅ |

- `steam_appid.txt` contains placeholder AppID 480 (Spacewar).
- Allow unsafe code enabled (Luban ByteBuf requirement).
- TMP font chain (`Assets/Codely/Fonts/`, all OFL): default ZCOOLKuaiLe-Regular SDF → fallback NotoSansSC → NotoSansTC → NotoSansJP → NotoSansKR → NotoSans, covering 13 languages.
- Font test scene `Assets/Scenes/FontTest.unity` (not in Build Settings).

## Conventions

- All UI text uses TextMeshPro with localization keys.
- All network scripts inherit `NetworkBehaviour`.
- System configs use ScriptableObject.
- Localization supports 13 languages: EN, zh-CN, zh-TW, JA, KO, RU, ES, PT-BR, DE, FR, PL, TR, IT.
- PowerShell batch edits of C# files must NOT use Set-Content (breaks string literals); use write_file or [IO.File]::WriteAllText+UTF8Encoding($false).

## Feature List (by module)

### Phase 1-3 (Init / Network Core / Player System)

| Module | Location | Description |
|---|---|---|
| NetworkConfig (SO) + Switcher | Network/Config/ | KCP/FizzySteamworks switching + Editor menu |
| CqwSteamManager / SteamLobbyManager | Network/Steam/ | SteamAPI lifecycle + lobby API |
| CqwNetworkManager + ProtocolAuthenticator | Network/Core/ | Persistent manager + protocol auth |
| SessionManager + ReconnectManager | Network/Session/ | Session API + auto-reconnect |
| PlayerIdentity/PlayerStats/BuffData | Network/Player/ | SyncVar identity/HP-stamina/Buff |
| NetworkObjectPool / CqwNetworkSceneManager | Network/Pool,Scene/ | Server-authoritative pooling + scene sync |
| NetworkTimeSync/NetworkAnimationSync | Network/Sync/ | Server time + Animator sync |
| RpcThrottler/MessageBatcher | Network/Messaging/ | Token bucket + message batching |
| NetworkBandwidthMonitor/NetworkSimulator/NetworkDiagnosticsCenter | Network/Diagnostics/ | Bandwidth + weak-net sim + quality |
| IPlayerInputSource + DefaultPlayerInputSource | Player/Input/ | Input source abstraction (KB+mouse / gamepad) |
| PlayerController + PlayerMovementSettings | Player/Controller/ | CharacterController movement/gravity/jump |
| PlayerCameraController | Player/Camera/ | 3-mode camera + occlusion |
| PlayerRegistry / TeamManager+TeamConfig | Player/Registry,Teams/ | Registry + teams |
| PlayerAnimatorBridge | Player/Animation/ | Animator bridge |
| LocalPlayerJoinManager | Player/LocalMulti/ | Local multiplayer (KB slot 0 + gamepad 1..N) |

### Phase 4 UI System (Assets/Scripts/UI/, namespace Cqw.MultilayerFramework.UI)

| Module | Location | Description |
|---|---|---|
| UIManager / UIPanelBase / UIPanelRegistry / UICanvasLayers | Core/ | Canvas layers + stack navigation + modal |
| UIBuilder | Core/ | Code-built UI helper |
| ConfirmDialog / ToastManager | Core/ | Modal confirm + toast |
| UIPanelBootstrap | Core/ | Auto-creates all panels (both scenes) |
| MainMenu/Settings/Lobby/Pause/SaveSlot/Hud/Chat/Loading | Panels/,Chat/,Hud/ | 8 panels (code-built + localization keys) |
| InteractionPromptScreen / InteractionPromptWorld | Interaction/ | Interaction prompts |
| IGameSettingsService + ServiceLocator | Services/ | Settings service interface |

### Phase 5 Save (Assets/Scripts/Save/, namespace Cqw.MultilayerFramework.Save)

SaveDataRoot/ISaveDataProvider (Core/), SaveSerializer (Core/), SaveFileIO (IO/, atomic+SHA256+backup), SaveMigrationService (IO/), FileSaveSlotService (Services/), AutoSaveScheduler (AutoSave/), SaveIOTaskQueue (Async/), SaveSystemBootstrap.

### Phase 6 Audio (Assets/Scripts/Audio/, namespace Cqw.MultilayerFramework.Audio)

AudioMixerController (4-channel+persistence+fallback), AudioPool (pooled+priority eviction), BgmPlayer (CrossFade), AudioSystemBootstrap.

### Phase 7 Localization (Assets/Scripts/Localization/, namespace Cqw.MultilayerFramework.Localization)

LanguageRegistry/LocalizationService (13 languages+fallback+persistence), LocalizationKeyHolder (TMP auto-refresh), LocalizationBootstrap, LocalizationKeyTool (Editor key tool). Language files: `Assets/Resources/Localization/language_{code}.json`.

### Phase 8 Common Systems (Assets/Scripts/Systems/, namespace Cqw.MultilayerFramework.Systems)

| Module | Location | Description |
|---|---|---|
| ObjectPool/UnityObjectPool/ObjectPoolHandle/ObjectPoolService | Pools/ | C# pool + Unity pool + using handle + stats + idle eviction |
| EventBus | Events/ | Typed events (boxing-free) + priority + Once + exception isolation |
| StateMachine/HierarchicalStateMachine/StateMachineRunner | StateMachine/ | Generic/Hierarchical state machine + frame driver |
| Scheduler/CooldownManager | Scheduling/ | Delay/repeat/cancel + cooldown |
| DataRegistry/DataRegistryHub | Registry/ | ID-based registry + duplicate policy + hub |
| ResourceHandle/ResourceManager | Resources/ | Async load + ref counting + delayed unload + preload |
| EffectHandle/EffectManager | Effects/ | Effect pool + auto recycle + follow |
| GameLogger/GameLoggerConfig | Logging/ | Leveled logging + background flush + 7-day retention |
| JsonGameSettingsService/GameSettingsKeys | Settings/ | JSON settings + migration + ApplyAll |
| SystemsBootstrap | (root) | 8-system unified bootstrap + Shutdown |
| SceneCameraGuard | (root) | Auto-disables scene camera when player camera activates |

### Phase 9 Interaction (Assets/Scripts/Interaction/, namespace Cqw.MultilayerFramework.Interaction)

IInteractable/InteractableBehaviour/InteractableData (Core/), InteractableRegistry (Core/), PlayerInteractor (root, scan+Cmd routing+prompt), TestLamp+TestLampSync/TestPickup+TestPickupSync (Test/, SyncVar regression interactables), Sample_Button+SampleButtonSync/Sample_Chest+SampleChestSync/SampleChestCounter (Samples/).

### Phase 10 Input (Assets/Scripts/Input/, namespace Cqw.MultilayerFramework.Input)

PlayerControls.inputactions+generated class (Settings/Input/), LocalInputRelay (bridge+device isolation+awareness), RebindManager (rebind+conflict+persistence), LocalInputManager (PIM takeover+self-create fallback+gamepad slot relay wiring), SplitScreenManager (1/2/3-4 split screen), KeybindsPanel (UI/Panels/).

### Phase 11 Dev Tools (Assets/Scripts/Tools/ + Editor/, namespace Cqw.MultilayerFramework.Tools)

NetworkSimulatorPresets (6 presets), CommandLineArgs+AutoMoveInputSource (CLI), NetworkDiagnosticsExporter (CSV); Editor/: NetworkSimulatorWindow, BandwidthMonitorWindow (60s bar chart), Inspectors (NetworkConfig/AudioMixerController/TeamConfig), BatchToolsWindow (TMP font/LocalizationKeyHolder batch), MultilayerMenus (Tools/Multilayer menu set), HealthCheck (one-click framework check), PanelWizard/InteractableWizard/SaveProviderWizard (code generation wizards).

### Phase 12 Verification & Docs

REGRESSION.md (12-module checklist), README.md (this file), delivery report.

## Quick Start

1. Open `Assets/Scenes/MainMenu.unity`, press Play.
2. Main menu → "Create Session" (Host) → enters GameScene, player 0 (KB+mouse) spawns automatically.
3. WASD move / mouse look / Space jump / E interact / C camera / V push-to-talk.
4. Gamepad press A to join → split screen auto-layout (side by side).
5. Walk near lamp/pickup → prompt shows → E interact (SyncVar sync).
6. Esc → pause; Settings panel for volume/language/keybinds.
7. Command line: `--netmode=host --address=127.0.0.1:7777 --automove --verbose`.
8. Tools menu: Tools/Multilayer (simulator/bandwidth/diagnostics/batch/weak-net presets).

## Configuration

| Config | Location |
|---|---|
| NetworkConfig | Assets/ScriptableObjects/NetworkConfig.asset |
| TeamConfig | Assets/ScriptableObjects/TeamConfig.asset |
| GameLoggerConfig | Assets/ScriptableObjects/GameLoggerConfig.asset |
| ValidationConfig | Assets/ScriptableObjects/ValidationConfig.asset |
| Interactable Data | Assets/ScriptableObjects/Interaction/*.asset |
| PlayerControls | Assets/Settings/Input/PlayerControls.inputactions |
| Language JSON | Assets/Resources/Localization/language_*.json |
| Runtime Settings | persistentDataPath/settings.json |
| Keybind Overrides | persistentDataPath/rebinds.json |
| Logs | persistentDataPath/Logs/game_yyyyMMdd.log |
| Diagnostics Export | persistentDataPath/Diagnostics/*.csv |

## Known Limitations

- Steam lobby/voice: AppID=480 placeholder, requires real AppId + Steam client for testing (code paths compile).
- Disconnect reconnection/scene sync: code paths exist, no real network-disconnect E2E test.
- Client-side interaction Cmd routing: Host-side verified, dual-machine testing pending.
- AudioMixer groups require manual Editor configuration (Unity 6 non-SO); controller has no-mixer fallback.
- HybridCLR Installer, Luban generator: placeholder, not yet run.
- InputSystem 1.20: PlayerInputManager.maxPlayerCount is read-only (default 4); playerJoinedEvent uses AddListener/RemoveListener.
- GamepadState.buttons bit layout (1.20 tested): buttonSouth=bit6.
- Mirror scene object lifecycle: scene interactables with NetworkIdentity are suppressed by Mirror before Host start (not registered), activated after (registered) — expected behavior.

## Extension Guide

- **New UI Panel**: Inherit `UIPanelBase` → register in UIPanelBootstrap → text uses `SetLocalizedText`.
- **New Interactable**: Inherit `InteractableBehaviour` (override OnInteract) → add NetworkIdentity (add Sync component for network sync) → place in scene, auto-registers.
- **New Save Provider**: Implement `ISaveDataProvider` → register in SaveDataRoot.
- **New Input Action**: Edit PlayerControls.inputactions (generator enabled) → LocalInputRelay.Tick reads.
- **New Weak-net Preset**: Append to NetworkSimulatorPresets.Table → menu/window auto-visible.
- **Wizards**: Use `Tools/Multilayer/Wizards/` for semi-automated generation of Panels, Interactables, and Save Providers.

## Troubleshooting

| Issue | Solution |
|---|---|
| TMP Chinese squares | Check Codely/Fonts font chain; FontTest scene for verification |
| PIM doesn't trigger join | Check LocalPlayerJoinManager exists (LocalInputManager self-creates fallback); real gamepad vs virtual device |
| Virtual device injection ineffective | Physical device overridden by native backend → RemoveDevice then use virtual device; empty report first to become current |
| devices changed no effect | After assignment, Player.Disable()→Enable() to force rebind (LocalInputRelay has this built-in) |
| Interactables not registered pre-Host | Mirror scene object lifecycle (activated after Host start), expected behavior |
| Editor Mirror noise logs | "get_time not allowed during serialization" is known Mirror editor noise |
| UAC0009/CS0105 warnings | DEVELOPMENT_BUILD deprecation / duplicate using, no functional impact |

## Progress

- [x] Phase 1: Project init + plugin import + TMP/font config (2026-09-23)
- [x] Phase 2: Network core (2026-09-23)
- [x] Phase 3: Player system (2026-09-24)
- [x] Phase 4: UI system (2026-09-27)
- [x] Phase 5: Save system (2026-09-27)
- [x] Phase 6: Audio system (2026-09-27)
- [x] Phase 7: Localization (2026-09-27)
- [x] Phase 8: Common systems (2026-09-28)
- [x] Phase 9: Interaction system (2026-09-28)
- [x] Phase 10: Input system (2026-09-28)
- [x] Phase 11: Dev tools (2026-09-28)
- [x] Phase 12: Verification & docs (2026-09-28)

Regression checklist: see [REGRESSION.md](REGRESSION.md).

## Framework Health Check

Menu path: `Tools/Multilayer/Health Check`

One-click check: compile status, Build Settings, NetworkManager, NetworkConfig, interactable count, UIRoot/UIManager, localization language count, today's log, settings file. Results output to Console and saved to `persistentDataPath/health_check.txt`.

Interpretation: all items should be found/OK/numbers. MISSING items indicate the corresponding system is not ready; check scene config or Bootstrap.

## Wizards

Menu `Tools/Multilayer/Wizards/`:
- **New UI Panel** (PanelWizard): Generates UIPanelBase subclass + 13-language placeholder keys; register in UIPanelBootstrap by adding `CreateAndRegister<T>(mgr, "Panel");`.
- **New Interactable** (InteractableWizard): Generates InteractableBehaviour subclass + InteractableData SO + localization key.
- **New Save Provider** (SaveProviderWizard): Generates ISaveDataProvider template.

## Server-Side Validation (InputValidator)
- Rate limiting: `ValidateRate(conn, actionKey, minInterval)` — same action from same connection within interval is rejected + violation count.
- Chat validation: `ValidateChat(conn, message)` — non-empty + length ≤ 128 (truncated if exceeded) + rate ≥ 0.5s.
- Ownership validation: `ValidateOwnership(identity, sender)` — returns false for non-owned operations.
- Config: `Assets/ScriptableObjects/ValidationConfig.asset`, violation policy (Ignore/Kick).
- Injection: `SystemsBootstrap.Awake` wires it up.

## Scene Camera Guard (SceneCameraGuard)
Automatically disables its own Camera and AudioListener when another enabled MainCamera-tagged camera (player camera) is detected, preventing dual rendering + dual listening. Attach to the scene's Main Camera GameObject.
