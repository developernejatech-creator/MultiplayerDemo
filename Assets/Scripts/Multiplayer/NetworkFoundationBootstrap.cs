using System.Threading.Tasks;
using Fusion;
using MultiplayerDemo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
#endif

namespace MultiplayerDemo.Multiplayer
{
    [RequireComponent(typeof(NetworkRunner))]
    [RequireComponent(typeof(NetworkSceneManagerDefault))]
    [RequireComponent(typeof(NetworkEvents))]
    public class NetworkFoundationBootstrap : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        private const int MaximumPlayers = 2;
        private const int VirtualPlayerJoinDelayMilliseconds = 3000;

        [SerializeField] private string sessionName = "PracticalExamSession";
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool autoStartInEditor = true;

        private NetworkRunner _networkRunner;
        private NetworkEvents _networkEvents;
        private bool _isStarting;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _jumpPressed;

        private void Awake()
        {
            _networkRunner = GetComponent<NetworkRunner>();
            _networkRunner.ProvideInput = true;

            _networkEvents = GetComponent<NetworkEvents>();
            _networkEvents.OnInput.AddListener(ProvideNetworkInput);
        }

        private async void Start()
        {
#if UNITY_EDITOR
            if (!autoStartInEditor)
            {
                return;
            }

            if (CurrentPlayer.IsMainEditor)
            {
                StartHost();
            }
            else
            {
                Debug.Log("[Fusion Foundation] Virtual Player will join in 3 seconds so its cloned project can finish initializing...");
                await Task.Delay(VirtualPlayerJoinDelayMilliseconds);

                if (this == null || !isActiveAndEnabled)
                {
                    return;
                }

                JoinSession();
            }
#endif
        }

        private void Update()
        {
            if (!_networkRunner.IsRunning || !Application.isFocused)
            {
                _moveInput = Vector2.zero;
                _lookInput = Vector2.zero;
                _jumpPressed = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                _moveInput = Vector2.zero;
                _jumpPressed = false;
                return;
            }

            _moveInput = new Vector2(
                ReadAxis(keyboard.aKey, keyboard.dKey),
                ReadAxis(keyboard.sKey, keyboard.wKey));

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                _lookInput += mouse.delta.ReadValue();
            }

            _jumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
        }

        private void ProvideNetworkInput(NetworkRunner runner, NetworkInput networkInput)
        {
            if (runner != _networkRunner)
            {
                return;
            }

            if (!Application.isFocused)
            {
                _moveInput = Vector2.zero;
                _lookInput = Vector2.zero;
                _jumpPressed = false;
            }

            NetworkButtons buttons = default;
            buttons.Set(PlayerInputButton.Jump, _jumpPressed);

            networkInput.Set(new PlayerNetworkInput
            {
                Move = Vector2.ClampMagnitude(_moveInput, 1f),
                LookDelta = _lookInput,
                Buttons = buttons
            });

            _lookInput = Vector2.zero;
            _jumpPressed = false;
        }

        private static float ReadAxis(KeyControl negative, KeyControl positive)
        {
            float value = 0f;

            if (negative.isPressed)
            {
                value -= 1f;
            }

            if (positive.isPressed)
            {
                value += 1f;
            }

            return value;
        }

        private void OnDestroy()
        {
            if (_networkEvents != null)
            {
                _networkEvents.OnInput.RemoveListener(ProvideNetworkInput);
            }
        }

        [ContextMenu("Start Host")]
        public void StartHost()
        {
            _ = StartSession(GameMode.Host);
        }

        [ContextMenu("Join Session")]
        public void JoinSession()
        {
            _ = StartSession(GameMode.Client);
        }

        private async Task StartSession(GameMode gameMode)
        {
            if (_isStarting || _networkRunner.IsRunning)
            {
                Debug.LogWarning("[Fusion Foundation] The NetworkRunner is already starting or running.");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[Fusion Foundation] Player Prefab is not assigned.");
                return;
            }

            _isStarting = true;
            Debug.Log($"[Fusion Foundation] Starting {gameMode} in session '{sessionName}'...");

            StartGameResult result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = sessionName,
                PlayerCount = MaximumPlayers,
                SceneManager = GetComponent<NetworkSceneManagerDefault>()
            });

            _isStarting = false;

            if (result.Ok)
            {
                Debug.Log($"[Fusion Foundation] {gameMode} connected to '{sessionName}'. Maximum players: {MaximumPlayers}.");
            }
            else
            {
                Debug.LogError($"[Fusion Foundation] Failed to start {gameMode}: {result}");
            }
        }

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log($"[Fusion Foundation] Player joined: {player}. Local player: {player == _networkRunner.LocalPlayer}.");

            if (!_networkRunner.IsServer)
            {
                return;
            }

            float spawnX = player.RawEncoded % 2 == 0 ? -1.5f : 1.5f;
            NetworkObject playerObject = _networkRunner.Spawn(
                playerPrefab.GetComponent<NetworkObject>(),
                new Vector3(spawnX, 1f, 0f),
                Quaternion.identity,
                player);

            _networkRunner.SetPlayerObject(player, playerObject);
        }

        public void PlayerLeft(PlayerRef player)
        {
            Debug.Log($"[Fusion Foundation] Player left: {player}.");

            if (_networkRunner.IsServer && _networkRunner.TryGetPlayerObject(player, out NetworkObject playerObject))
            {
                _networkRunner.Despawn(playerObject);
            }
        }
    }
}
