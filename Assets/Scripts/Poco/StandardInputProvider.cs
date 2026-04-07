using UnityEngine;
using UnityEngine.InputSystem;

public class StandardInputProvider : IInputProvider
{
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    // 새로 추가된 액션
    private InputAction cleaveAction;

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

        // 마우스 좌클릭 바인딩
        cleaveAction = new InputAction("Cleave", binding: "<Mouse>/leftButton");
    }

    public Vector2 MoveInput => moveAction.ReadValue<Vector2>();
    public Vector2 LookInput => lookAction.ReadValue<Vector2>();
    public bool JumpTriggered => jumpAction.WasPressedThisFrame();
    public bool DashHeld => dashAction.IsInProgress();

    // 새로 추가된 프로퍼티 (버튼을 누르고 있는 동안 true)
    public bool CleaveHeld => cleaveAction.IsInProgress();

    public void Enable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        cleaveAction.Enable(); // 활성화
    }

    public void Disable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
        cleaveAction.Disable(); // 비활성화
    }
}