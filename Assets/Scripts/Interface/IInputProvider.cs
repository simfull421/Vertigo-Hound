using UnityEngine;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpTriggered { get; }
    bool DashHeld { get; }
    
    void Enable();
    void Disable();
}