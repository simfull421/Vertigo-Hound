// ============================================================================
// PhysicsMotor.cs — Rigidbody 물리 제어 전담 컴포넌트
// 모든 물리 연산(가속, 중력 반전, Momentum 보존)을 이 클래스에 집중합니다.
// ★ 점프: Y축 속도를 초기화한 후 Impulse를 적용하여 확실하게 작동
// ★ 중력 반전: 캐릭터 회전 없이 커스텀 중력 방향만 전환 (카메라가 연출 담당)
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
        [Tooltip("바닥 감지용 SphereCast 거리")]
        [SerializeField] private float groundCheckDistance = 0.15f;

        [Tooltip("바닥 감지 SphereCast 반지름")]
        [SerializeField] private float groundCheckRadius = 0.3f;

        [Tooltip("바닥으로 인식할 레이어")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("벽으로 인식할 레이어")]
        [SerializeField] private LayerMask wallLayer;

        [Tooltip("천장으로 인식할 레이어")]
        [SerializeField] private LayerMask ceilingLayer;

        [Tooltip("벽 감지 Raycast 거리")]
        [SerializeField] private float wallCheckDistance = 0.8f;

        [Tooltip("천장 감지 Raycast 거리")]
        [SerializeField] private float ceilingCheckDistance = 0.3f;

        // ──────────────────────────────────────────────
        // 내부 참조 (Awake에서 캐싱)
        // ──────────────────────────────────────────────

        private Rigidbody rb;
        private CapsuleCollider capsule;
        private float defaultCapsuleHeight;
        private Vector3 defaultCapsuleCenter;
        private bool isGravityInverted;

        /// <summary>SphereCast/Raycast에서 플레이어 자신을 제외하기 위한 마스크</summary>
        private LayerMask playerExcludeMask;

        // ──────────────────────────────────────────────
        // 외부에서 읽기 전용으로 접근하는 프로퍼티
        // ──────────────────────────────────────────────

        /// <summary>현재 Rigidbody 속도</summary>
        public Vector3 Velocity => rb.linearVelocity;

        /// <summary>현재 진행 방향 속도 크기 (수평 Momentum)</summary>
        public float CurrentSpeed => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

        /// <summary>최대 질주 속도 (State에서 비율 계산용)</summary>
        public float MaxSprintSpeed => maxSprintSpeed;

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

        /// <summary>
        /// 커스텀 중력 활성화 여부. false면 ApplyCustomGravity()가 작동하지 않음.
        /// ★ WallRunState에서 false로 설정하여 기본 중력(25f)이 나선 하강을 방해하지 않게 함.
        /// </summary>
        public bool EnableCustomGravity { get; set; } = true;

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

            // 물리 회전은 직접 제어 (Rigidbody가 알아서 굴리지 않도록)
            rb.freezeRotation = true;

            // ★ 플레이어 자신의 레이어를 제외하는 마스크 생성
            // SphereCast/Raycast가 자기 자신의 Collider를 바닥/벽으로 오인하는 것을 방지
            playerExcludeMask = ~(1 << gameObject.layer);

            Debug.Log($"[PhysicsMotor] Awake — PlayerLayer={gameObject.layer}, " +
                      $"GroundLayer={groundLayer.value}, WallLayer={wallLayer.value}, " +
                      $"CeilingLayer={ceilingLayer.value}");
        }

        private void FixedUpdate()
        {
            ApplyCustomGravity();
            UpdateGroundCheck();
            UpdateWallCheck();
            UpdateCeilingCheck();
        }

        // ══════════════════════════════════════════════
        // 이동 (Movement)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 수평 이동. Y축 속도는 절대 건드리지 않습니다.
        /// ★ 핵심: rb.linearVelocity.y를 그대로 보존하고 X/Z만 설정
        /// </summary>
        public void Move(Vector3 direction)
        {
            Vector3 targetVelocity = direction * maxSprintSpeed;
            Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 desiredHorizontal = Vector3.MoveTowards(
                currentHorizontal,
                targetVelocity,
                acceleration * Time.deltaTime
            );

            // ★ Y축 속도를 반드시 보존 — 점프/낙하 velocity를 절대 덮어쓰지 않음
            rb.linearVelocity = new Vector3(desiredHorizontal.x, rb.linearVelocity.y, desiredHorizontal.z);
        }

        /// <summary>
        /// 공중 좌우 미세 조작 (Air Strafing).
        /// </summary>
        public void AirStrafe(Vector3 direction, float strafeForce)
        {
            rb.AddForce(direction * strafeForce, ForceMode.Acceleration);
        }

        // ══════════════════════════════════════════════
        // 점프 (Jump) — 확실하게 작동하도록 Y속도 초기화 후 Impulse
        // ══════════════════════════════════════════════

        /// <summary>
        /// 바닥 점프. Y축 속도를 0으로 리셋한 뒤 직접 velocity에 점프 속도를 기록합니다.
        /// ★ AddForce(Impulse) 대신 velocity 직접 설정 — 질량 무관하게 확실한 점프
        /// </summary>
        public void Jump()
        {
            Vector3 vel = rb.linearVelocity;

            // ★ Y축 속도를 jumpForce로 직접 설정 (리셋 후 설정을 원자적으로)
            float jumpY = isGravityInverted ? -jumpForce : jumpForce;
            rb.linearVelocity = new Vector3(vel.x, jumpY, vel.z);

            Debug.Log($"[PhysicsMotor] Jump() — 직접 velocity.y={jumpY:F1} 설정 완료, " +
                      $"최종 velocity={rb.linearVelocity}");
        }

        /// <summary>
        /// 벽 차기(Wall Jump). 벽 법선 기반 대각선 Impulse + Momentum 100% 보존.
        /// </summary>
        public void WallJump()
        {
            // 1) Momentum 저장
            float preservedSpeed = CurrentSpeed;

            Debug.Log($"[PhysicsMotor] WallJump() — preservedSpeed={preservedSpeed:F2}, " +
                      $"wallNormal={LastWallNormal}, force={jumpForce * wallJumpMultiplier:F1}");

            // 2) 수직 속도 초기화
            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);

            // 3) 법선 + 위쪽을 합성한 대각선 방향
            Vector3 upDir = isGravityInverted ? Vector3.down : Vector3.up;
            Vector3 jumpDir = (LastWallNormal + upDir).normalized;

            // 4) 강한 Impulse
            rb.AddForce(jumpDir * (jumpForce * wallJumpMultiplier), ForceMode.Impulse);

            // 5) Momentum 보존
            PreserveMomentum(preservedSpeed);

            Debug.Log($"[PhysicsMotor] WallJump 완료 — jumpDir={jumpDir}, velocity={rb.linearVelocity}");
        }

        // ══════════════════════════════════════════════
        // Momentum 보존 (당구공 로직)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 수평 진행 속도 크기를 targetSpeed로 복원합니다.
        /// 방향은 유지, 크기만 보정.
        /// </summary>
        public void PreserveMomentum(float targetSpeed)
        {
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontal.sqrMagnitude < 0.01f) return;

            Vector3 preserved = horizontal.normalized * targetSpeed;
            rb.linearVelocity = new Vector3(preserved.x, rb.linearVelocity.y, preserved.z);
        }

        // ══════════════════════════════════════════════
        // 중력 제어 (Gravity Control)
        // ══════════════════════════════════════════════

        /// <summary>매 FixedUpdate에서 호출되는 커스텀 중력.</summary>
        private void ApplyCustomGravity()
        {
            // ★ WallRun 등에서 기본 중력을 끄면 여기서 즉시 리턴
            if (!EnableCustomGravity) return;

            Vector3 gravityDir = isGravityInverted ? Vector3.up : Vector3.down;
            rb.AddForce(gravityDir * gravityMagnitude, ForceMode.Acceleration);
        }

        /// <summary>
        /// 중력을 180도 반전합니다 (천장 달라붙기).
        /// ★ 캐릭터 Transform은 회전하지 않음 — 카메라 연출(CameraController)이 담당
        /// </summary>
        public void InvertGravity()
        {
            isGravityInverted = true;
            // 반전 직후 수직 속도 초기화 (부드러운 전환)
            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
        }

        /// <summary>중력을 원래 방향으로 복구합니다.</summary>
        public void RestoreGravity()
        {
            isGravityInverted = false;
            // 복구 직후 수직 속도 초기화 (부드러운 전환)
            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
        }

        /// <summary>벽 타기 중 약한 하향 힘을 적용합니다 (나선형 하강).</summary>
        public void ApplyWallRunDownforce()
        {
            rb.AddForce(Vector3.down * wallRunDownforce, ForceMode.Acceleration);
        }

        // ══════════════════════════════════════════════
        // 속도 전환 (QuadRecovery용)
        // ══════════════════════════════════════════════

        /// <summary>Y축 추락 속도를 전진 속도로 100% 치환합니다.</summary>
        public void ConvertVerticalToForward()
        {
            float fallingSpeed = Mathf.Abs(rb.linearVelocity.y);
            Vector3 fwd = transform.forward;
            rb.linearVelocity = fwd * fallingSpeed;
        }

        // ══════════════════════════════════════════════
        // 슬라이딩 충돌체 제어
        // ══════════════════════════════════════════════

        public void ShrinkCollider()
        {
            capsule.height = defaultCapsuleHeight * slideColliderRatio;
            capsule.center = new Vector3(
                defaultCapsuleCenter.x,
                defaultCapsuleCenter.y * slideColliderRatio,
                defaultCapsuleCenter.z
            );
        }

        public void RestoreCollider()
        {
            capsule.height = defaultCapsuleHeight;
            capsule.center = defaultCapsuleCenter;
        }

        public void SetFriction(float friction)
        {
            if (capsule.material == null)
                capsule.material = new PhysicsMaterial();

            capsule.material.dynamicFriction = friction;
            capsule.material.staticFriction = friction;
        }

        // ══════════════════════════════════════════════
        // 접지 · 벽 · 천장 감지 (SphereCast / Raycast)
        // ══════════════════════════════════════════════

        private void UpdateGroundCheck()
        {
            // ★ 캡슐 Pivot(중앙)이 아닌 발바닥에서 센서 시작
            // 캡슐 하단 = transform.position - (capsule.height * 0.5f)
            // 거기서 groundCheckRadius만큼 위로 올려 SphereCast 시작점 확보
            float halfHeight = capsule.height * 0.5f;
            Vector3 feetPos = transform.position - Vector3.up * halfHeight;
            Vector3 origin = feetPos + Vector3.up * groundCheckRadius;

            Vector3 direction = isGravityInverted ? Vector3.up : Vector3.down;

            // LayerMask 검증: 인스펙터 미설정 시 이름 기반 폴백
            int mask = groundLayer.value;
            if (mask == 0)
            {
                mask = LayerMask.GetMask("Ground", "Wall", "Ceiling");
                Debug.LogWarning($"[PhysicsMotor] groundLayer 미설정! 폴백 마스크 사용: {mask}");
            }
            mask &= playerExcludeMask;

            IsGrounded = Physics.SphereCast(
                origin,
                groundCheckRadius,
                direction,
                out _,
                groundCheckDistance,
                mask
            );
        }

        private void UpdateWallCheck()
        {
            // ★ 가슴 높이에서 벽 감지 (transform.position = 캡슐 중앙이 아닌 Pivot)
            // Pivot(발바닥) + 살짝 위 = 가슴 높이
            // 캡슐 높이의 40% 정도가 가슴에 해당
            Vector3 pos = transform.position + Vector3.up * (capsule.height * 0.4f);

            int combinedWallMask = wallLayer & playerExcludeMask;

            bool hitRight = Physics.Raycast(pos, transform.right, out RaycastHit rightHit, wallCheckDistance, combinedWallMask);
            bool hitLeft = Physics.Raycast(pos, -transform.right, out RaycastHit leftHit, wallCheckDistance, combinedWallMask);

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
            Vector3 origin = transform.position + Vector3.up * capsule.height;
            Vector3 direction = isGravityInverted ? Vector3.down : Vector3.up;

            // ★ ceilingLayer AND 플레이어 자신 레이어 제외
            int combinedCeilingMask = ceilingLayer & playerExcludeMask;
            IsTouchingCeiling = Physics.Raycast(origin, direction, ceilingCheckDistance, combinedCeilingMask);
        }

        // ══════════════════════════════════════════════
        // 속도 직접 설정 (특수 상황용)
        // ══════════════════════════════════════════════

        public void SetVelocity(Vector3 velocity) => rb.linearVelocity = velocity;

        public void ResetVerticalVelocity()
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }
}
