using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpTriggered { get; }
    bool DashHeld { get; }
    void Enable();
    void Disable();
}

public class StandardInputProvider : IInputProvider
{
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    public Vector2 MoveInput => moveAction.ReadValue<Vector2>();
    public Vector2 LookInput => lookAction.ReadValue<Vector2>();
    
    // 유니티 6의 새로운 Input System에서 단발성 트리거 및 홀드 체크
    public bool JumpTriggered => jumpAction.WasPressedThisFrame();
    public bool DashHeld => dashAction.IsPressed();

    public StandardInputProvider()
    {
        // POCO(에셋 없이 스크립트로만) 기반 InputAction 인스턴스화
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", binding: "<Gamepad>/rightStick");
        lookAction.AddBinding("<Mouse>/delta");

        jumpAction = new InputAction("Jump", binding: "<Gamepad>/buttonSouth");
        jumpAction.AddBinding("<Keyboard>/space");

        dashAction = new InputAction("Dash", binding: "<Gamepad>/rightTrigger");
        dashAction.AddBinding("<Keyboard>/leftShift");
    }

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
