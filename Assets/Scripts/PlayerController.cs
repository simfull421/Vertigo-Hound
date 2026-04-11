using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement (Walk & Run)")]
    public float walkSpeed = 6f;
    public float runInitialSpeed = 10f; 
    public float runMaxSpeed = 18f;     
    public float runChargeTime = 3f;    
    
    public float groundAccel = 80f;
    public float airAccel = 30f;
    public float jumpForce = 12f;

    [Header("Descent & Landing")]
    public float minFallSpeedForDescent = -5f; // 이 속도 이상 떨어질 때만 하강 레이캐스트 가동
    public float minAirTimeForRoll = 0.5f;     // 이 시간 이상 공중에 있어야 착지 시 구르기 발동

    [Header("Wall Run & Jump")]
    public LayerMask wallLayerMask;
    public float wallCheckDistance = 1.5f;
    public float wallRunSpeed = 15f;
    public float wallRunGravity = 2f; 
    public float wallJumpForce = 15f;

    private bool isWallLeft;
    private bool isWallRight;
    private bool isWallRunning;
    private bool wasWallRunning;
    private Vector3 wallNormal;
    private float wallJumpTimer = 0f;

    [Header("Camera & Look Hierarchy")]
    [Tooltip("마우스 위아래(Pitch) 회전을 전담하는 피벗입니다.")]
    public Transform cameraPitchPivot;
    public float mouseSensitivity = 0.1f;
    private float xRotation = 0f;

    [Header("Camera Actions (Jump Tricks)")]
    [Tooltip("액션 회전 전담 스크립트를 할당합니다.")]
    public CameraActionController cameraActionController;

    [Header("Ground Check")]
    public LayerMask groundMask;
    private bool isGrounded;
    private bool wasGrounded;
    private CapsuleCollider capsule;
    private Vector3 groundNormal = Vector3.up;
    [Header("Camera Effects")]
    public CameraJuiceController juiceController;
    private Rigidbody rb;
    private bool jumpIntended;
    
    private IInputProvider input;
    private float currentRunTime = 0f;
    private float currentAirTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        input = new StandardInputProvider();
        input.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        wasGrounded = true; 
    }

    void OnDestroy()
    {
        input?.Disable();
    }

    void Update()
    {
        CheckGrounded();
        
        if (wallJumpTimer > 0f) wallJumpTimer -= Time.deltaTime;
        CheckWall();

        HandleLook();
        UpdateSprintJuice();
        if (!isGrounded)
        {
            currentAirTime += Time.deltaTime;
            CheckDescentState();
        }
        else
        {
            currentAirTime = 0f;
            if (cameraActionController != null)
            {
                cameraActionController.ResetDescentPitch();
            }
        }

        if (input.JumpTriggered)
        {
            jumpIntended = true;
        }
    }
    void UpdateSprintJuice()
    {
        // currentRunTime은 Dash(달리기) 유지 시간이며, runChargeTime(최고 속도에 도달하는 시간)으로 나누어 가속 진행도(0.0 ~ 1.0)를 구합니다.
        float normalizedAccel = Mathf.Clamp01(currentRunTime / runChargeTime);

        // 쥬스 컨트롤러가 연결되어 있다면, 매 프레임 현재 가속도 상태를 전달!
        if (juiceController != null)
        {
            if (isWallRunning)
            {
                juiceController.UpdateWallRunJuice(isWallRight, normalizedAccel);
            }
            else
            {
                juiceController.UpdateSprintJuice(normalizedAccel, isGrounded);
            }
        }
    }
    void FixedUpdate()
    {
        if (isWallRunning)
        {
            WallRunMovement();
        }
        else
        {
            rb.useGravity = true;
            MovePlayer();
        }

        if (jumpIntended)
        {
            if (isWallRunning)
            {
                WallJump();
            }
            else
            {
                Jump();
            }
            jumpIntended = false;
        }
    }

    private void CheckWall()
    {
        if (wallJumpTimer > 0f) 
        {
            isWallLeft = false;
            isWallRight = false;
            isWallRunning = false;
            return;
        }

        isWallRight = Physics.Raycast(transform.position, transform.right, out RaycastHit rightHit, wallCheckDistance, wallLayerMask);
        isWallLeft = Physics.Raycast(transform.position, -transform.right, out RaycastHit leftHit, wallCheckDistance, wallLayerMask);

        if (isWallRight) wallNormal = rightHit.normal;
        else if (isWallLeft) wallNormal = leftHit.normal;
        else wallNormal = Vector3.zero;

        wasWallRunning = isWallRunning;
        // 공중에 있고, 벽이 있고, 전진(W) 중일 때만 벽타기
        isWallRunning = !isGrounded && (isWallLeft || isWallRight) && (input.MoveInput.y > 0);

        if (isWallRunning && !wasWallRunning)
        {
            if (juiceController != null) juiceController.TriggerWallAttachJuice(isWallRight);
        }
    }

    private void WallRunMovement()
    {
        rb.useGravity = false;
        
        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);

        // 진행 방향과 카메라(몸체) 시선 내적이 음수면 반대로 교정
        if (Vector3.Dot(transform.forward, wallForward) < 0)
        {
            wallForward = -wallForward; 
        }

        // 고정 하강 속도(-wallRunGravity) 적용
        rb.linearVelocity = new Vector3(wallForward.x * wallRunSpeed, -wallRunGravity, wallForward.z * wallRunSpeed);
    }

    private void WallJump()
    {
        rb.useGravity = true;
        // Y 속도 초기화하여 안정적인 곡선 유도
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // 핵심 핑퐁 점프: 카메라 정면 + 벽 법선 + 수직
        Vector3 jumpDir = (cameraPitchPivot.forward * 0.5f) + (wallNormal * 1.5f) + (Vector3.up * 1.0f);
        rb.AddForce(jumpDir.normalized * wallJumpForce, ForceMode.Impulse);

        wallJumpTimer = 0.2f;
        isWallRunning = false;
    }

    private void CheckDescentState()
    {
        // 최적화: 특정 속도 이상으로 빠르게 하강 중일 때만 연산
        if (rb.linearVelocity.y < minFallSpeedForDescent)
        {
            /*
             * 공중 고개 숙임 (강제 시야 뺏김) 주석 처리하여
             * 떨어지는 내내 마우스로 자유롭게 다음 타겟을 찾을 수 있도록 수정
             * if (cameraActionController != null)
             * {
             *     cameraActionController.UpdateDescent(currentAirTime, rb.linearVelocity.y);
             * }
             */

            // 낙하 공기저항 쉐이크만 별도로 남김
            if (juiceController != null)
            {
                juiceController.UpdateDescentShake(currentAirTime, rb.linearVelocity.y);
            }
        }
    }

    private void CheckGrounded()
    {
        wasGrounded = isGrounded;

        Vector3 bottom = capsule.bounds.center - new Vector3(0, capsule.bounds.extents.y, 0);
        bool hitSphere = Physics.CheckSphere(bottom + Vector3.up * 0.3f, 0.45f, groundMask);

        if (hitSphere)
        {
            if (Physics.Raycast(capsule.bounds.center, Vector3.down, out RaycastHit hit, capsule.bounds.extents.y + 0.5f, groundMask))
            {
                groundNormal = hit.normal;
            }
            else
            {
                groundNormal = Vector3.up;
            }

            // 벽점프 등 수직 오브젝트에 닿아서 구르기가 나가는 현상 방지:
            // 경사각이 50도 이하인 확실한 바닥(Floor)일 때만 Grounded로 판정
            if (Vector3.Angle(Vector3.up, groundNormal) <= 50f)
            {
                isGrounded = true;
                if (!wasGrounded)
                {
                    OnLanded();
                }
            }
            else
            {
                isGrounded = false;
            }
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private void OnLanded()
    {
        if (cameraActionController != null)
        {
            // 치명적 높이에서 착지했을 경우 타격감 구르기 구사
            if (currentAirTime >= minAirTimeForRoll)
            {
                cameraActionController.TriggerLandingRoll();
            }
            else
            {
                // 안전한 높이에서 안착하면 진행 중인 공중 회전을 조기 코루틴 종료시킴
                cameraActionController.InterruptAction();
            }
        }
    }

    private void HandleLook()
    {
        Vector2 lookInput = input.LookInput;
        
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        if (cameraPitchPivot != null)
        {
            cameraPitchPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = input.MoveInput;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        float currentTargetSpeed = walkSpeed;

        if (input.DashHeld && isMoving)
        {
            currentRunTime += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(currentRunTime / runChargeTime);
            currentTargetSpeed = Mathf.Lerp(runInitialSpeed, runMaxSpeed, progress);
        }
        else
        {
            currentRunTime = 0f; 
            if (!isMoving) currentTargetSpeed = 0f;
        }

        Vector3 targetVelocity = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized * currentTargetSpeed;
        Vector3 currentXZVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        float accelRate = isGrounded ? groundAccel : airAccel;
        Vector3 newXZVelocity = Vector3.MoveTowards(currentXZVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newXZVelocity.x, rb.linearVelocity.y, newXZVelocity.z);
    }

    private void Jump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 jumpDir = Vector3.Lerp(Vector3.up, groundNormal, 0.6f).normalized;
            rb.AddForce(jumpDir * jumpForce, ForceMode.Impulse);

            float v0 = jumpForce / rb.mass;
            float g = Physics.gravity.magnitude; 
            float timeToApex = v0 / g;

            if (cameraActionController != null)
            {
                 cameraActionController.TriggerRandomPattern(timeToApex);
            }
        }
    }
}