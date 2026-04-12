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
    public PlayerWallRebound wallRebound = new PlayerWallRebound();

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
        wallRebound.Initialize(this);
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
        wallRebound.UpdateModule(); // Rebound UI 피드백 감지 루프 작동

        if (InputProv.JumpTriggered)
        {
            _jumpIntended = true;
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

        if (_jumpIntended)
        {
            // 1순위: 막다른 코너나 벽을 만나서 튕겨 벗어나는 행동 최우선 처리
            wallRebound.HandleJump();
            if (wallRebound.JustRebounded)
            {
                _jumpIntended = false;
                return;
            }

            // 2순위: Vault (뜀틀) - 이미 Update 루프에서 캐싱된 결과를 활용
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