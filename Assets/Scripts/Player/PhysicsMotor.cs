// ============================================================================
// PhysicsMotor.cs — Rigidbody 물리 제어 전담 컴포넌트
// 모든 물리 연산(가속, 중력 반전, Momentum 보존)을 이 클래스에 집중합니다.
// [RequireComponent(typeof(Rigidbody))] : 자동으로 Rigidbody를 부착합니다.
// [RequireComponent(typeof(CapsuleCollider))] : 슬라이딩 시 충돌체 축소에 필요합니다.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PhysicsMotor : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 조절 변수 (기획자가 에디터에서 수정 가능)
        // ──────────────────────────────────────────────

        [Header("=== 질주 (Sprint) ===")]
        [Tooltip("최대 질주 속도 (m/s)")]
        [SerializeField] private float maxSprintSpeed = 20f;

        [Tooltip("가속도 (m/s²). 높을수록 빠르게 최고 속도에 도달")]
        [SerializeField] private float acceleration = 15f;

        [Header("=== 점프 ===")]
        [Tooltip("바닥 점프 힘 (Impulse)")]
        [SerializeField] private float jumpForce = 10f;

        [Tooltip("벽 차기(Wall Jump) 힘 배율. 바닥 점프보다 강해야 함")]
        [SerializeField] private float wallJumpMultiplier = 1.5f;

        [Header("=== 중력 ===")]
        [Tooltip("기본 중력 크기 (양수값). Physics.gravity 대체용")]
        [SerializeField] private float gravityMagnitude = 25f;

        [Tooltip("벽 타기 중 약한 하향 힘 (나선 하강용)")]
        [SerializeField] private float wallRunDownforce = 3f;

        [Header("=== 슬라이딩 ===")]
        [Tooltip("슬라이딩 시 충돌체 높이 (기본 높이의 비율, 0.5 = 절반)")]
        [SerializeField] private float slideColliderRatio = 0.5f;

        [Header("=== 접지 감지 ===")]
        [Tooltip("바닥 감지용 Raycast 거리")]
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Tooltip("바닥으로 인식할 레이어")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("벽으로 인식할 레이어")]
        [SerializeField] private LayerMask wallLayer;

        [Tooltip("천장으로 인식할 레이어")]
        [SerializeField] private LayerMask ceilingLayer;

        // ──────────────────────────────────────────────
        // 내부 참조 (Awake에서 캐싱)
        // ──────────────────────────────────────────────

        private Rigidbody rb;
        private CapsuleCollider capsule;
        private float defaultCapsuleHeight;
        private Vector3 defaultCapsuleCenter;
        private bool isGravityInverted;

        // ──────────────────────────────────────────────
        // 외부에서 읽기 전용으로 접근하는 프로퍼티
        // ──────────────────────────────────────────────

        /// <summary>현재 Rigidbody 속도</summary>
        public Vector3 Velocity => rb.linearVelocity;

        /// <summary>현재 진행 방향 속도 크기 (Momentum)</summary>
        public float CurrentSpeed => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

        /// <summary>중력이 반전되어 있는가?</summary>
        public bool IsGravityInverted => isGravityInverted;

        /// <summary>바닥에 닿아 있는가?</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>최근 벽 충돌 법선 벡터 (WallJump 방향 계산용)</summary>
        public Vector3 LastWallNormal { get; private set; }

        /// <summary>벽에 닿아 있는가? (Raycast 기반)</summary>
        public bool IsTouchingWall { get; private set; }

        /// <summary>천장에 닿아 있는가? (Raycast 기반)</summary>
        public bool IsTouchingCeiling { get; private set; }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            defaultCapsuleHeight = capsule.height;
            defaultCapsuleCenter = capsule.center;

            // 유니티 기본 중력을 끄고 직접 제어
            rb.useGravity = false;
        }

        private void FixedUpdate()
        {
            ApplyCustomGravity();
            UpdateGroundCheck();
            UpdateWallCheck();
            UpdateCeilingCheck();
        }

        // ──────────────────────────────────────────────
        // 이동 (Movement)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 수평 이동 입력을 받아 가속합니다.
        /// 최대 속도를 초과하지 않도록 클램프합니다.
        /// </summary>
        /// <param name="direction">카메라 기준 이동 방향 (정규화된 XZ 벡터)</param>
        public void Move(Vector3 direction)
        {
            Vector3 targetVelocity = direction * maxSprintSpeed;
            Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 velocityChange = Vector3.MoveTowards(currentHorizontal, targetVelocity, acceleration * Time.fixedDeltaTime) - currentHorizontal;
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>
        /// 공중에서의 좌우 회피 이동 (Air Strafing).
        /// FreeFallState, AirborneState에서 사용합니다.
        /// </summary>
        /// <param name="direction">좌우 방향 벡터</param>
        /// <param name="strafeForce">공중 조작력</param>
        public void AirStrafe(Vector3 direction, float strafeForce)
        {
            rb.AddForce(direction * strafeForce, ForceMode.Acceleration);
        }

        // ──────────────────────────────────────────────
        // 점프 (Jump)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 바닥 점프. 수직 방향으로 Impulse를 가합니다.
        /// 중력 반전 상태에서는 아래 방향으로 점프합니다.
        /// </summary>
        public void Jump()
        {
            Vector3 jumpDirection = isGravityInverted ? Vector3.down : Vector3.up;
            rb.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
        }

        /// <summary>
        /// 벽 차기(Wall Jump). 벽 표면의 법선 벡터를 이용해
        /// 대각선 위쪽으로 강하게 튕겨 나갑니다.
        /// ★ 핵심: 진행 방향 Momentum은 100% 보존됩니다.
        /// </summary>
        public void WallJump()
        {
            // 1) 현재 수평 속도의 크기(Momentum)를 저장
            float preservedSpeed = CurrentSpeed;

            // 2) 벽 법선 + 위쪽을 합성한 대각선 방향 계산
            Vector3 upDirection = isGravityInverted ? Vector3.down : Vector3.up;
            Vector3 jumpDirection = (LastWallNormal + upDirection).normalized;

            // 3) 법선 방향으로 강한 Impulse 적용
            rb.AddForce(jumpDirection * (jumpForce * wallJumpMultiplier), ForceMode.Impulse);

            // 4) Momentum 보존: 수평 속도 크기를 원래 값으로 복원
            PreserveMomentum(preservedSpeed);
        }

        // ──────────────────────────────────────────────
        // Momentum 보존 (당구공 로직)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 상태 전환 시 수평 진행 속도(Momentum)를 지정된 크기로 복원합니다.
        /// 방향은 현재 수평 이동 방향을 유지하고 크기만 보정합니다.
        /// </summary>
        /// <param name="targetSpeed">보존해야 할 속도 크기</param>
        public void PreserveMomentum(float targetSpeed)
        {
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontal.sqrMagnitude < 0.01f) return;

            Vector3 preservedHorizontal = horizontal.normalized * targetSpeed;
            rb.linearVelocity = new Vector3(preservedHorizontal.x, rb.linearVelocity.y, preservedHorizontal.z);
        }

        // ──────────────────────────────────────────────
        // 중력 제어 (Gravity Control)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 매 FixedUpdate에서 호출되는 커스텀 중력.
        /// 반전 여부에 따라 방향이 바뀝니다.
        /// </summary>
        private void ApplyCustomGravity()
        {
            Vector3 gravityDirection = isGravityInverted ? Vector3.up : Vector3.down;
            rb.AddForce(gravityDirection * gravityMagnitude, ForceMode.Acceleration);
        }

        /// <summary>
        /// 중력을 180도 반전합니다 (천장 달라붙기).
        /// 캐릭터의 로컬 Up 방향도 뒤집어야 합니다.
        /// </summary>
        public void InvertGravity()
        {
            isGravityInverted = true;
            // 캐릭터 로컬 축 반전 (시각적으로 거꾸로 서기)
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(euler.x, euler.y, 180f);
        }

        /// <summary>
        /// 중력을 원래 방향으로 복구합니다.
        /// </summary>
        public void RestoreGravity()
        {
            isGravityInverted = false;
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(euler.x, euler.y, 0f);
        }

        /// <summary>
        /// 벽 타기 중 약한 하향 힘을 적용합니다 (나선형 하강).
        /// </summary>
        public void ApplyWallRunDownforce()
        {
            rb.AddForce(Vector3.down * wallRunDownforce, ForceMode.Acceleration);
        }

        // ──────────────────────────────────────────────
        // 속도 전환 (QuadRecovery용)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Y축 추락 속도를 Z축 전진 속도로 100% 치환합니다.
        /// 멈춤 없이 질주로 이어지는 핵심 로직입니다.
        /// </summary>
        public void ConvertVerticalToForward()
        {
            float fallingSpeed = Mathf.Abs(rb.linearVelocity.y);
            Vector3 forwardDirection = transform.forward;
            rb.linearVelocity = forwardDirection * fallingSpeed;
        }

        // ──────────────────────────────────────────────
        // 슬라이딩 충돌체 제어
        // ──────────────────────────────────────────────

        /// <summary>
        /// 슬라이딩 진입: 충돌체 높이를 절반으로 줄입니다.
        /// </summary>
        public void ShrinkCollider()
        {
            capsule.height = defaultCapsuleHeight * slideColliderRatio;
            capsule.center = new Vector3(
                defaultCapsuleCenter.x,
                defaultCapsuleCenter.y * slideColliderRatio,
                defaultCapsuleCenter.z
            );
        }

        /// <summary>
        /// 슬라이딩 종료: 충돌체를 원래 크기로 복구합니다.
        /// </summary>
        public void RestoreCollider()
        {
            capsule.height = defaultCapsuleHeight;
            capsule.center = defaultCapsuleCenter;
        }

        /// <summary>
        /// 슬라이딩 시 마찰력을 0으로 만듭니다.
        /// </summary>
        public void SetFriction(float friction)
        {
            if (capsule.material == null)
            {
                capsule.material = new PhysicsMaterial();
            }
            capsule.material.dynamicFriction = friction;
            capsule.material.staticFriction = friction;
        }

        // ──────────────────────────────────────────────
        // 접지 · 벽 · 천장 감지 (Raycast)
        // ──────────────────────────────────────────────

        private void UpdateGroundCheck()
        {
            Vector3 origin = transform.position;
            Vector3 direction = isGravityInverted ? Vector3.up : Vector3.down;
            IsGrounded = Physics.Raycast(origin, direction, groundCheckDistance, groundLayer);
        }

        private void UpdateWallCheck()
        {
            // 좌/우 Raycast로 벽 감지
            bool hitRight = Physics.Raycast(transform.position, transform.right, out RaycastHit rightHit, 1f, wallLayer);
            bool hitLeft = Physics.Raycast(transform.position, -transform.right, out RaycastHit leftHit, 1f, wallLayer);

            if (hitRight)
            {
                IsTouchingWall = true;
                LastWallNormal = rightHit.normal;
            }
            else if (hitLeft)
            {
                IsTouchingWall = true;
                LastWallNormal = leftHit.normal;
            }
            else
            {
                IsTouchingWall = false;
            }
        }

        private void UpdateCeilingCheck()
        {
            Vector3 direction = isGravityInverted ? Vector3.down : Vector3.up;
            IsTouchingCeiling = Physics.Raycast(transform.position, direction, groundCheckDistance + capsule.height, ceilingLayer);
        }

        // ──────────────────────────────────────────────
        // 속도 직접 설정 (특수 상황용)
        // ──────────────────────────────────────────────

        /// <summary>속도를 직접 설정합니다 (주의해서 사용)</summary>
        public void SetVelocity(Vector3 velocity)
        {
            rb.linearVelocity = velocity;
        }

        /// <summary>Y축 속도만 0으로 리셋합니다.</summary>
        public void ResetVerticalVelocity()
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }
}
