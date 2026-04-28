using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpTriggered { get; }
    bool DashHeld { get; }
    bool SlideHeld { get; }
    bool CrouchHeld { get; }
    bool AimHeld { get; }
    bool FireTriggered { get; }
    bool ReloadTriggered { get; }
    bool Weapon1Triggered { get; }
    bool Weapon2Triggered { get; }
    bool InteractTriggered { get; }
    
    void Enable();
    void Disable();
}

public class StandardInputProvider : IInputProvider
{
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private InputAction slideAction;
    private InputAction crouchAction;
    private InputAction aimAction;
    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction weapon1Action;
    private InputAction weapon2Action;
    private InputAction interactAction;

    public Vector2 MoveInput => moveAction.ReadValue<Vector2>();
    public Vector2 LookInput => lookAction.ReadValue<Vector2>();
    
    // 유니티 6의 새로운 Input System에서 단발성 트리거 및 홀드 체크
    public bool JumpTriggered => jumpAction.WasPressedThisFrame();
    public bool DashHeld => dashAction.IsPressed();
    public bool SlideHeld => slideAction.IsPressed();
    public bool CrouchHeld => crouchAction.IsPressed();
    public bool AimHeld => aimAction.IsPressed();
    public bool FireTriggered => fireAction.WasPressedThisFrame();
    public bool ReloadTriggered => reloadAction.WasPressedThisFrame();
    public bool Weapon1Triggered => weapon1Action.WasPressedThisFrame();
    public bool Weapon2Triggered => weapon2Action.WasPressedThisFrame();
    public bool InteractTriggered => interactAction.WasPressedThisFrame();

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

        slideAction = new InputAction("Slide", binding: "<Gamepad>/buttonEast");
        slideAction.AddBinding("<Keyboard>/leftCtrl");

        crouchAction = new InputAction("Crouch", binding: "<Keyboard>/c");

        aimAction = new InputAction("Aim", binding: "<Gamepad>/leftTrigger");
        aimAction.AddBinding("<Mouse>/rightButton");

        fireAction = new InputAction("Fire", binding: "<Gamepad>/rightTrigger");
        fireAction.AddBinding("<Mouse>/leftButton");

        reloadAction = new InputAction("Reload", binding: "<Gamepad>/buttonWest");
        reloadAction.AddBinding("<Keyboard>/r");

        weapon1Action = new InputAction("Weapon1", binding: "<Keyboard>/1");
        weapon2Action = new InputAction("Weapon2", binding: "<Keyboard>/2");

        interactAction = new InputAction("Interact", binding: "<Gamepad>/buttonWest");
        interactAction.AddBinding("<Keyboard>/e");
    }

    public void Enable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        slideAction.Enable();
        crouchAction.Enable();
        aimAction.Enable();
        fireAction.Enable();
        reloadAction.Enable();
        weapon1Action.Enable();
        weapon2Action.Enable();
        interactAction.Enable();
    }

    public void Disable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
        slideAction.Disable();
        crouchAction.Disable();
        aimAction.Disable();
        fireAction.Disable();
        reloadAction.Disable();
        weapon1Action.Disable();
        weapon2Action.Disable();
        interactAction.Disable();
    }
}
