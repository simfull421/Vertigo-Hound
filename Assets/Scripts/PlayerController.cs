using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour, IPlayerState
{
    [Header("Physics & Movement (Snappy)")]
    public float maxSpeed = 15f;
    public float dashSpeed = 35f; // 대시 최고 속도
    public float groundAccel = 150f;
    public float airAccel = 30f;
    public float jumpForce = 10f;

    [Header("Dash & FOV Effects")]
    public Camera mainCamera;
    public float normalFOV = 90f;
    public float dashFOV = 115f; // 대시 시 찢어질 시야각
    public float fovSpeed = 10f;

    [Header("Ground Check")]
    public LayerMask groundMask;
    private bool isGrounded;
    private CapsuleCollider capsule;

    [Header("Camera & Look")]
    public Transform cameraRig;
    public float mouseSensitivity = 0.1f;
    private float xRotation = 0f;

    private Rigidbody rb;
    private bool jumpIntended;

    private IInputProvider input;
    private MomentumSystem momentum; // 매니저 없이 POCO로 모멘텀 제어

    [Header("Wall Jump")]
    public float wallCheckDistance = 0.8f; // 캡슐 콜라이더 반경보다 살짝 크게 설정 (여유있게 0.8f)
    private bool isWalled;
    private Vector3 wallNormal;

    [Header("References")]
    public TrendyVisualController visualController; // Inspector에서 연결
    public TrendyCleaveController cleaveController; // 추가된 참조 (DI 용)

    // IPlayerState 구현
    public bool IsGrounded => isGrounded;
    public Rigidbody PlayerRigidbody => rb; // 추가된 프로퍼티

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        input = new StandardInputProvider();
        input.Enable();

        momentum = new MomentumSystem(); // 의존성 주입
        
        // [DI 핵심] 비주얼 컨트롤러에 의존성 주입
        if (visualController != null)
        {
            visualController.Initialize(momentum, input);
        }

        // [DI 핵심] 가르기 컨트롤러에 의존성 주입
        if (cleaveController != null)
        {
            cleaveController.Initialize(input, this); // IPlayerState 주입
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDestroy()
    {
        input?.Disable();
    }

    void Update()
    {
        CheckGrounded();
        CheckWall(); 
        HandleLook();
        HandleCameraFOV();

        if (input.JumpTriggered)
        {
            jumpIntended = true;
        }
    }

    void FixedUpdate()
    {
        MovePlayer();

        if (jumpIntended)
        {
            Jump();
            jumpIntended = false;
        }
    }

    private Vector3 groundNormal = Vector3.up;

    private void CheckGrounded()
    {
        // 경사면 감지를 위해 Sphere 크기를 좀 더 키우고 위치를 상단으로 보정
        Vector3 bottom = capsule.bounds.center - new Vector3(0, capsule.bounds.extents.y, 0);
        isGrounded = Physics.CheckSphere(bottom + Vector3.up * 0.3f, 0.45f, groundMask);

        // 점프 시 씹힘 방지를 위해 정확한 지면 Normal 각도를 추출합니다.
        if (isGrounded)
        {
            if (Physics.Raycast(capsule.bounds.center, Vector3.down, out RaycastHit hit, capsule.bounds.extents.y + 0.5f, groundMask))
            {
                groundNormal = hit.normal;
            }
            else
            {
                groundNormal = Vector3.up;
            }
        }
    }

    private void CheckWall()
    {
        isWalled = false;

        // 플레이어의 오른쪽, 왼쪽, 앞쪽으로 짧은 레이저를 쏴서 벽 감지
        Vector3[] directions = { transform.right, -transform.right, transform.forward };

        foreach (var dir in directions)
        {
            // groundMask를 그대로 활용하여 큐브(벽) 레이어를 감지
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, groundMask))
            {
                isWalled = true;
                wallNormal = hit.normal; // 튕겨나갈 때 사용할 벽의 수직 방향 저장
                return; // 하나라도 벽에 닿았으면 즉시 종료
            }
        }
    }

    private void HandleLook()
    {
        // 가르기 모드(조준 중)일 때는 화면을 얼리고 마우스로 화면상 라인을 그려야 하므로 시점 이동을 잠깐 막음
        if (cleaveController != null && cleaveController.isAiming) return;

        Vector2 lookInput = input.LookInput;
        
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraRig.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleCameraFOV()
    {
        // 대시 상태면 FOV를 늘리고, 아니면 원래대로 복구
        bool isDashing = input.DashHeld && momentum.Value > 10f;
        float targetFOV = isDashing ? dashFOV : normalFOV;
        
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, fovSpeed * Time.deltaTime);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = input.MoveInput;

        // Shift는 이제 무조건 달리기 (모멘텀 제한 해제)
        float currentTargetSpeed = input.DashHeld ? dashSpeed : maxSpeed;

        Vector3 targetVelocity = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized * currentTargetSpeed;
        Vector3 currentXZVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // 경사면에서 미끄러질 때 조작감을 잃지 않도록 가속도를 더 높게 설정 가능
        float accelRate = isGrounded ? groundAccel : airAccel;
        Vector3 newXZVelocity = Vector3.MoveTowards(currentXZVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newXZVelocity.x, rb.linearVelocity.y, newXZVelocity.z);

        // 모멘텀은 뒤에서 조용히 쌓임 (가르기를 위한 에너지원)
        float flatSpeed = currentXZVelocity.magnitude;
        momentum.Calculate(flatSpeed, maxSpeed, input.DashHeld, Time.fixedDeltaTime);
    }

    private void Jump()
    {
        // 관성(X, Z)을 유지하면서 점프를 보장하기 위한 개선된 로직
        if (isGrounded || isWalled)
        {
            // Y축 속도만 0으로 초기화하여 하강력 등만 상쇄 (X, Z 보존)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            if (isWalled && !isGrounded)
            {
                // 벽 점프 - 벽에서 밀쳐내며 비스듬하게 위로
                Vector3 jumpDir = (Vector3.up + wallNormal).normalized;
                rb.AddForce(jumpDir * jumpForce * 1.2f, ForceMode.Impulse);
                momentum.AddBoost(20f);
            }
            else
            {
                // 일반/경사면 점프 - 마찰로 인한 씹힘을 날리기 위해 바닥의 기울기(Normal) 방향을 섞어서 추진력 적용
                Vector3 jumpDir = Vector3.Lerp(Vector3.up, groundNormal, 0.6f).normalized;
                rb.AddForce(jumpDir * jumpForce, ForceMode.Impulse);
            }
        }
    }

    void OnDrawGizmos()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            Vector3 bottom = cap.bounds.center - new Vector3(0, cap.bounds.extents.y, 0);
            Vector3 sphereCenter = bottom + Vector3.up * 0.3f;
            
            // 바닥 감지 (초록색 = Grounded, 빨간색 = 공중)
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(sphereCenter, 0.45f);
        }

        // 벽 감지 레이저 표시
        Gizmos.color = isWalled ? Color.green : Color.yellow;
        Vector3[] directions = { transform.right, -transform.right, transform.forward };
        foreach (var dir in directions)
        {
            Gizmos.DrawRay(transform.position, dir * wallCheckDistance);
        }
    }
}