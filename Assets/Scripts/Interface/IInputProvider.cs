using UnityEngine;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpTriggered { get; }
    bool DashHeld { get; }
    // 추가된 가르기(조준) 입력 속성
    bool CleaveHeld { get; }
    void Enable();
    void Disable();
}