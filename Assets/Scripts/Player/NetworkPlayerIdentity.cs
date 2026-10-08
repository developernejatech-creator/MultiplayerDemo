using Fusion;
using UnityEngine;

namespace MultiplayerDemo.Player
{
    public class NetworkPlayerIdentity : NetworkBehaviour
    {
        private Camera _playerCamera;
        private AudioListener _audioListener;
        private Renderer _capsuleRenderer;

        public bool IsLocalPlayer { get; private set; }

        public override void Spawned()
        {
            IsLocalPlayer = Object.HasInputAuthority;

            _playerCamera = GetComponentInChildren<Camera>(true);
            _audioListener = GetComponentInChildren<AudioListener>(true);
            _capsuleRenderer = GetComponent<Renderer>();

            if (_playerCamera != null)
            {
                _playerCamera.enabled = IsLocalPlayer;
            }

            if (_audioListener != null)
            {
                _audioListener.enabled = IsLocalPlayer;
            }

            gameObject.name = IsLocalPlayer
                ? $"Player {Object.InputAuthority} (LOCAL)"
                : $"Player {Object.InputAuthority} (REMOTE)";

            if (_capsuleRenderer != null)
            {
                _capsuleRenderer.material.color = IsLocalPlayer
                    ? new Color(0.2f, 0.85f, 0.35f)
                    : new Color(0.25f, 0.55f, 1f);
            }

            Debug.Log($"[Fusion Foundation] Spawned {gameObject.name}. HasInputAuthority={Object.HasInputAuthority}.");
        }
    }
}
