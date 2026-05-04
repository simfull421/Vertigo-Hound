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
    [Tooltip("발소리를 담당하는 PlayerAudioManager를 여기에 드래그 앤 드롭하세요.")]
    public PlayerAudioManager audioManager;

    // [추가] 뷰모델 오브젝트 할당용 변수
    [Header("Viewmodel Settings")]
    [Tooltip("뷰모델 카메라 하위에 있는 총기/팔 오브젝트 (GunUpper)를 연결하세요.")]
    public GameObject viewmodelGun;
    [Tooltip("뷰모델 카메라 하위에 있는 맨손 오브젝트 (RunUpper)")]
    public GameObject runUpper;
    [Tooltip("월드 모델(본체)에 붙은 Animator. VaultIK 등 메인 IK 시스템 전용. 뷰모델 Animator를 절대 연결하지 마세요!")]
    public Animator worldModelAnimator;
    /// <summary>RunUpper의 Animator 컨포넌트. Awake에서 캐싱합니다.</summary>
    [HideInInspector] public Animator runUpperAnimator;
    [Header("Viewmodel Weapon")]
    public ViewmodelGunController gunController; // 인스펙터에서 연결
    [Header("Modules")]
    public PlayerMovement movement = new PlayerMovement();
    public PlayerWallKick wallKick = new PlayerWallKick();
    public PlayerSlider slider = new PlayerSlider();
    public PlayerVault vault = new PlayerVault();
    public PlayerRamp ramp = new PlayerRamp();
    public PlayerAnimatorHandler animatorHandler = new PlayerAnimatorHandler();
    public PlayerWallTouchIK wallTouchIK = new PlayerWallTouchIK();
    public PlayerVaultIK vaultIK = new PlayerVaultIK();
    public PlayerBreachModule breachModule = new PlayerBreachModule();
    public PlayerInteractionModule interactModule = new PlayerInteractionModule();
   
    private bool _jumpIntended;
    private bool _jumpFromSlide;

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        Capsule = GetComponent<CapsuleCollider>();

        InputProv = new StandardInputProvider();
        InputProv.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        movement.Initialize(this);
        wallKick.Initialize(this);
        slider.Initialize(this);
        vault.Initialize(this);
        ramp.Initialize(this);

        // [순서 중요] RunUpper Animator 캐싱은 animatorHandler.Initialize보다 먼저 실행되어야 함
        // animatorHandler.Initialize 내부에서 _hub.runUpperAnimator를 참조하기 때문
        if (runUpper != null)
        {
            runUpperAnimator = runUpper.GetComponent<Animator>();
            if (runUpperAnimator == null)
                Debug.LogError("[PlayerController] RunUpper에 Animator 컴포넌트가 없습니다!");
        }
        else
        {
            Debug.LogError("[PlayerController] runUpper가 연결되지 않았습니다!");
        }

        animatorHandler.Initialize(this);

        // WallTouchIK: RunUpper(맨손 뷰모델)의 애니메이터로 IK 구동
        wallTouchIK.Initialize(this, runUpperAnimator);

        // VaultIK: 월드 모델의 메인 Animator 전용 (아바타 Humanoid IK 필수)
        if (worldModelAnimator == null)
            Debug.LogError("[PlayerController] worldModelAnimator가 연결되지 않았습니다! VaultIK가 동작하지 않습니다.");
        vaultIK.Initialize(this, worldModelAnimator);

        breachModule.Initialize(this);
        interactModule.Initialize(this);

        if (gunController != null) gunController.Initialize(this);
    }

    void OnDestroy()
    {
        InputProv?.Disable();
    }

    /// <summary>
    /// 애니메이션 클립의 'PlayFootstep' 이벤트로부터 신호를 직접 수신하는 본체 수신처입니다.
    /// (애니메이터가 붙은 오브젝트에 이 메서드가 있어야 Failed to call AnimationEvent 에러가 발생하지 않습니다)
    /// </summary>
    public void PlayFootstep(string side)
    {
        // 1단계: 애니메이션 이벤트 전파 (쥬스 시스템)
        if (this.juiceController != null)
        {
            this.juiceController.OnFootstepTriggered(side);
        }

        // 2단계: 오디오 서비스 실행 (직속 할당된 오디오 매니저 사용)
        if (this.audioManager != null)
        {
            float currentSpeed = (Rb != null) ? Rb.linearVelocity.magnitude : 0f;
            this.audioManager.PlayFootstepAudio(side, currentSpeed);
        }
        else
        {
            Debug.LogWarning("[Footstep Warning] PlayerController에 PlayerAudioManager가 할당되지 않았습니다!");
        }
    }




    void Update()
    {
        movement.UpdateModule();
        wallKick.UpdateModule();

        if (InputProv.JumpTriggered)
        {
            _jumpIntended = true;
            _jumpFromSlide = slider.IsSliding;

            if (movement.IsGrounded)
            {
                animatorHandler.TriggerJump();
            }
        }

        slider.UpdateModule();
        vault.UpdateModule(); // Smart Reticle용 상시 감지 루프 작동
        ramp.UpdateModule();
        animatorHandler.UpdateModule();
        wallTouchIK.UpdateModule();
        vaultIK.UpdateModule();
        gunController.UpdateModule();
        interactModule.UpdateModule();
    }

    // [추가] 애니메이터 연산이 끝난 후 본 스케일 0을 덮어씌우는 타이밍
  void LateUpdate()
    {
        animatorHandler.LateUpdateModule(); // 원래 있던 파쿠르 팔 날리기 로직
        if (gunController != null) gunController.LateUpdateModule(); // 방금 만든 정조준 정렬 로직
    }

    void FixedUpdate()
    {
        bool shouldPreserveSlideMomentum = slider.IsSliding || _jumpFromSlide;
        
        if (vault.IsVaulting)
        {
            // Vault는 내부 FixedUpdate 코루틴에서 전적으로 이동을 통제하므로 다른 모듈 로직을 배제합니다.
        }
        else if (shouldPreserveSlideMomentum)
        {
            if (slider.IsSliding)
            {
                slider.FixedUpdateModule();
            }
        }
        else
        {
            movement.FixedUpdateModule();
        }

        if (!vault.IsVaulting)
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

                if (wallKick.CanWallKick)
                {
                    wallKick.HandleJump();
                }
                else if (shouldPreserveSlideMomentum)
                {
                    slider.HandleJump();
                }
                else
                {
                    movement.HandleJump();
                }
            }
            _jumpIntended = false;
            _jumpFromSlide = false;
        }
    }
}
