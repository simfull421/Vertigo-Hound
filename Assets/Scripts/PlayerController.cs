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

    // [추가] 뷰모델 오브젝트 할당용 변수
    [Header("Viewmodel Settings")]
    [Tooltip("뷰모델 카메라 하위에 있는 총기/팔 오브젝트 (GunUpper)를 연결하세요.")]
    public GameObject viewmodelGun;
    [Header("Viewmodel Weapon")]
    public ViewmodelGunController gunController; // 인스펙터에서 연결
    [Header("Modules")]
    public PlayerMovement movement = new PlayerMovement();
    public PlayerWallRunner wallRunner = new PlayerWallRunner();
    public PlayerSlider slider = new PlayerSlider();
    public PlayerVault vault = new PlayerVault();
    public PlayerRamp ramp = new PlayerRamp();
    public PlayerAnimatorHandler animatorHandler = new PlayerAnimatorHandler();
    public PlayerWallTouchIK wallTouchIK = new PlayerWallTouchIK();
    public PlayerVaultIK vaultIK = new PlayerVaultIK();
   
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
        vaultIK.Initialize(this, animatorHandler.animator);
        if (gunController != null) gunController.Initialize(this);
    }

    void OnDestroy()
    {
        InputProv?.Disable();
    }

    /// <summary>
    /// 애니메이션 이벤트 포워더로부터 발소리 신호를 수신합니다.
    /// </summary>
    public void OnPlayerFootstep(string side)
    {
        if (juiceController != null)
        {
            juiceController.OnFootstepTriggered(side);
        }
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
        vaultIK.UpdateModule();
        gunController.UpdateModule();
            
        if (InputProv.JumpTriggered)
        {
            _jumpIntended = true;

            if (movement.IsGrounded)
            {
                animatorHandler.TriggerJump();
            }
        }
    }

    // [추가] 애니메이터 연산이 끝난 후 본 스케일 0을 덮어씌우는 타이밍
  void LateUpdate()
    {
        animatorHandler.LateUpdateModule(); // 원래 있던 파쿠르 팔 날리기 로직
        if (gunController != null) gunController.LateUpdateModule(); // 방금 만든 정조준 정렬 로직
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

        if (!vault.IsVaulting && !wallRunner.IsWallRunning)
        {
            ramp.FixedUpdateModule(_jumpIntended);
        }

        if (_jumpIntended)
        {
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