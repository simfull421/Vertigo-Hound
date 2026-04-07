using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Physics & Movement (Snappy)")]
    public float maxSpeed = 15f;
    public float groundAccel = 150f;
    public float airAccel = 30f;
    public float jumpForce = 10f;

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

    // 핵심: 인터페이스를 통한 느슨한 결합
    private IInputProvider input;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // [의존성 주입 - DI]
        // 나중에 VContainer나 Zenject 같은 DI 프레임워크를 쓰면 이 부분도 외부에서 주입해줍니다.
        // 현재는 수동으로 생성하여 주입합니다.
        input = new StandardInputProvider();
        input.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDestroy()
    {
        // 객체 파괴 시 입력 리스너 해제
        input?.Disable();
    }

    void Update()
    {
        CheckGrounded();
        HandleLook();

        // 싱글톤(Instance) 없이 인터페이스를 통해 깔끔하게 입력값 수신
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

    private void CheckGrounded()
    {
        Vector3 bottom = capsule.bounds.center - new Vector3(0, capsule.bounds.extents.y, 0);
        isGrounded = Physics.CheckSphere(bottom + Vector3.up * 0.1f, 0.15f, groundMask);
    }

    private void HandleLook()
    {
        Vector2 lookInput = input.LookInput;
        
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraRig.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = input.MoveInput;

        Vector3 targetVelocity = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized * maxSpeed;
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
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
    }
}