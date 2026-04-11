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
        HandleLook();

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

    void FixedUpdate()
    {
        MovePlayer();

        if (jumpIntended)
        {
            Jump();
            jumpIntended = false;
        }
    }

    private void CheckDescentState()
    {
        // 최적화: 특정 속도 이상으로 빠르게 하강 중일 때만 연산
        if (rb.linearVelocity.y < minFallSpeedForDescent)
        {
            if (cameraActionController != null)
            {
                // 바닥 레이캐스트 대신 순수 체공 시간(Air Time)을 넘김
                cameraActionController.UpdateDescent(currentAirTime, rb.linearVelocity.y);
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