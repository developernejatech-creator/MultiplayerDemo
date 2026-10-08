using Fusion;
using UnityEngine;

namespace MultiplayerDemo.Player
{
    public enum PlayerInputButton
    {
        Jump
    }

    public struct PlayerNetworkInput : INetworkInput
    {
        public Vector2 Move;
        public Vector2 LookDelta;
        public NetworkButtons Buttons;
    }
}
