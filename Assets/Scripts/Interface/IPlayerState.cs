using UnityEngine;

public interface IPlayerState
{
    bool IsGrounded { get; }
    Rigidbody PlayerRigidbody { get; }
}
