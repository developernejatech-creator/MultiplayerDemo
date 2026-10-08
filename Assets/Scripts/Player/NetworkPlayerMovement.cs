using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MultiplayerDemo.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkCharacterController))]
    public class NetworkPlayerMovement : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float gravity = 20f;

        [Header("Mouse Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Range(-89f, 0f)] private float minimumLookAngle = -80f;
        [SerializeField, Range(0f, 89f)] private float maximumLookAngle = 80f;

        [Networked] private float CameraPitch { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }

        private NetworkCharacterController _networkController;
        private Transform _cameraTransform;
        private bool _isLocalPlayer;

        private void Awake()
        {
            _networkController = GetComponent<NetworkCharacterController>();
            _cameraTransform = transform.Find("Camera");
        }

        public override void Spawned()
        {
            _isLocalPlayer = Object.HasInputAuthority;

            _networkController.maxSpeed = movementSpeed;
            _networkController.gravity = -gravity;
            _networkController.rotationSpeed = 0f;

            if (_isLocalPlayer && Application.isFocused)
            {
                LockCursor();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out PlayerNetworkInput input))
            {
                return;
            }

            float yawChange = input.LookDelta.x * mouseSensitivity;
            float pitchChange = input.LookDelta.y * mouseSensitivity;

            transform.Rotate(0f, yawChange, 0f);
            CameraPitch = Mathf.Clamp(
                CameraPitch - pitchChange,
                minimumLookAngle,
                maximumLookAngle);

            if (input.Buttons.WasPressed(PreviousButtons, PlayerInputButton.Jump))
            {
                float jumpImpulse = Mathf.Sqrt(2f * gravity * jumpHeight);
                _networkController.Jump(false, jumpImpulse);
            }

            Vector3 movementDirection =
                transform.right * input.Move.x +
                transform.forward * input.Move.y;

            _networkController.Move(movementDirection);
            PreviousButtons = input.Buttons;
        }

        public override void Render()
        {
            if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            }
        }

        private void Update()
        {
            if (!_isLocalPlayer || Keyboard.current == null || Mouse.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UnlockCursor();
            }
            else if (Application.isFocused && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_isLocalPlayer)
            {
                UnlockCursor();
            }

            _isLocalPlayer = false;
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
