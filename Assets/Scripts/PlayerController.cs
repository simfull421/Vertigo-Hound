using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public Rigidbody Rb { get; private set; }
    public CapsuleCollider Capsule { get; private set; }
    public IInputProvider InputProv { get; private set; }

    [Header("Camera & Action Control")]
    public CameraActionController cameraActionController;
    public CameraJuiceController juiceController;

    [Header("Modules")]
    public PlayerMovement movement = new PlayerMovement();
    public PlayerWallRunner wallRunner = new PlayerWallRunner();
    public PlayerSlider slider = new PlayerSlider();
    public PlayerVault vault = new PlayerVault();
    public PlayerRamp ramp = new PlayerRamp();
    public PlayerAnimatorHandler animatorHandler = new PlayerAnimatorHandler();
    public PlayerWallTouchIK wallTouchIK = new PlayerWallTouchIK();
   
    private bool _jumpIntended;

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        Capsule = GetComponent<CapsuleCollider>();

        InputProv = new StandardInputProvider();
        InputProv.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        movement.Initialize(this);
        wallRunner.Initialize(this);
        slider.Initialize(this);
        vault.Initialize(this);
        ramp.Initialize(this);
        animatorHandler.Initialize(this);
        wallTouchIK.Initialize(this, animatorHandler.animator);
    }

    void OnDestroy()
    {
        InputProv?.Disable();
    }

    void Update()
    {
        movement.UpdateModule();
        wallRunner.UpdateModule();
        slider.UpdateModule();
        vault.UpdateModule(); // Smart Reticle용 상시 감지 루프 작동
        ramp.UpdateModule();
        animatorHandler.UpdateModule();
        wallTouchIK.UpdateModule();
   
        if (InputProv.JumpTriggered)
        {
            _jumpIntended = true;

            // 허공 점프 시 애니메이션 중복 발생(무한 점프) 방지
            if (movement.IsGrounded)
            {
                animatorHandler.TriggerJump();
            }
        }

    }

    void FixedUpdate()
    {
        if (vault.IsVaulting)
        {
            // Vault는 내부 FixedUpdate 코루틴에서 전적으로 이동을 통제하므로 다른 모듈 로직을 배제합니다.
        }
        else if (wallRunner.IsWallRunning)
        {
            wallRunner.FixedUpdateModule();
        }
        else if (slider.IsSliding)
        {
            slider.FixedUpdateModule();
        }
        else
        {
            movement.FixedUpdateModule();
        }

        // 경사면 후처리: 걷기/슬라이딩 모듈 실행 후 경사면 물리를 덧어씌움
        if (!vault.IsVaulting && !wallRunner.IsWallRunning)
        {
            ramp.FixedUpdateModule(_jumpIntended);
        }

        if (_jumpIntended)
        {
    

            //  Vault (뜀틀) - 이미 Update 루프에서 캐싱된 결과를 활용
            if (!vault.IsVaulting)
            {
                vault.HandleJump();
                if (vault.IsVaulting) 
                {
                    _jumpIntended = false;
                    return;
                }

                if (wallRunner.IsWallRunning)
                {
                    wallRunner.HandleJump();
                }
                else if (slider.IsSliding)
                {
                    slider.HandleJump();
                }
                else
                {
                    movement.HandleJump();
                }
            }
            _jumpIntended = false;
        }
    }
}