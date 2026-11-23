using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>Character type placeholder (Phase 3 extends to full character definitions).</summary>
    public enum CharacterType
    {
        Default = 0,
        Chicken = 1,
        Robo = 2
    }

    /// <summary>
    /// Player identity: nickname/character type/SteamID/team (SyncVars, server-authoritative, client read-only).
    /// Nickname changes auto-refresh overhead TMP; TeamId used by TeamManager/PlayerRegistry.
    /// </summary>
    [AddComponentMenu("Cqw/Network/PlayerIdentity")]
    public class PlayerIdentity : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnNicknameChanged))]
        string nickname;

        [SyncVar(hook = nameof(OnCharacterTypeChanged))]
        int characterType;

        [SyncVar(hook = nameof(OnSteamIdChanged))]
        string steamId;

        [SyncVar(hook = nameof(OnTeamChanged))]
        int teamId = -1;

        /// <summary>Player nickname (displayName alias).</summary>
        public string Nickname => nickname;
        /// <summary>Display name (=nickname; unified UI entry point).</summary>
        public string DisplayName => string.IsNullOrEmpty(nickname) ? "Player" : nickname;
        public CharacterType CharacterType => (CharacterType)characterType;
        public string SteamId => steamId;
        /// <summary>Team ID (-1=unassigned; definitions in TeamManager/TeamConfig).</summary>
        public int TeamId => teamId;

        /// <summary>Local player wrapper (Mirror isLocalPlayer).</summary>
        public bool IsLocalPlayer => isLocalPlayer;

        /// <summary>Nickname change notification (client refreshes UI).</summary>
        public event System.Action<string> OnNicknameUpdated;
        /// <summary>Any identity field change (character type/SteamID/team).</summary>
        public event System.Action OnIdentityChanged;
        /// <summary>Team change (TeamManager subscribes).</summary>
        public event System.Action<int> OnTeamUpdated;

        TMPro.TextMeshPro nicknameLabel;

        [Server]
        public void SetNickname(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = "Player";
            nickname = value;
        }

        [Server]
        public void SetCharacterType(CharacterType type) => characterType = (int)type;

        [Server]
        public void SetSteamId(string value) => steamId = value ?? "";

        [Server]
        public void SetTeamId(int value) => teamId = value;

        /// <summary>Client requests nickname restore (called by ReconnectManager after reconnect).</summary>
        [Command]
        public void CmdRestoreNickname(string value) => SetNickname(value);

        // ---- Client hooks: forward events + refresh overhead text ----

        void OnNicknameChanged(string oldV, string newV)
        {
            OnNicknameUpdated?.Invoke(newV);
            RefreshNicknameLabel(newV);
        }

        void OnCharacterTypeChanged(int oldV, int newV) => OnIdentityChanged?.Invoke();

        void OnSteamIdChanged(string oldV, int newV) => OnIdentityChanged?.Invoke();

        void OnSteamIdChanged(string oldV, string newV) => OnIdentityChanged?.Invoke();

        void OnTeamChanged(int oldV, int newV)
        {
            OnTeamUpdated?.Invoke(newV);
            OnIdentityChanged?.Invoke();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnNicknameUpdated?.Invoke(nickname);
            RefreshNicknameLabel(nickname);
            // Host-side OnStartServer already registered; pure client registers here
            if (!isServer) Player.PlayerRegistry.Register(this);
        }

        void RefreshNicknameLabel(string value)
        {
            if (nicknameLabel == null)
                nicknameLabel = GetComponentInChildren<TMPro.TextMeshPro>(true);
            if (nicknameLabel != null)
                nicknameLabel.text = string.IsNullOrEmpty(value) ? "Player" : value;
        }

        // ---- Registry integration (T3-06): spawn register / destroy unregister, no scene scanning ----

        public override void OnStartServer()
        {
            base.OnStartServer();
            Player.PlayerRegistry.Register(this);
        }

        void OnDestroy()
        {
            Player.PlayerRegistry.Unregister(this);
        }
    }
}
