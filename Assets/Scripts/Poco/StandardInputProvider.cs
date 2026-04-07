using UnityEngine;
using UnityEngine.InputSystem;

public class StandardInputProvider : IInputProvider
{
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    // 생성자에서 액션 초기화
    public StandardInputProvider()
    {
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", binding: "<Mouse>/delta");
        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        dashAction = new InputAction("Dash", binding: "<Keyboard>/leftShift");
    }

    // 인터페이스 프로퍼티 구현 (람다식으로 호출될 때마다 최신 값 반환)
    public Vector2 MoveInput => moveAction.ReadValue<Vector2>();
    public Vector2 LookInput => lookAction.ReadValue<Vector2>();
    public bool JumpTriggered => jumpAction.WasPressedThisFrame();
    public bool DashHeld => dashAction.IsInProgress();

    public void Enable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
    }

    public void Disable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }
}