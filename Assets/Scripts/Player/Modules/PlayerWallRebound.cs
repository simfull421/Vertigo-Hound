using UnityEngine;
using System;

[Serializable]
public sealed class PlayerWallRebound
{
    [Header("Wall Rebound Settings")]
    public float wallCheckDistance = 1.5f; // 판정 거리를 넉넉히 주어 여유 확보
    public float minReboundSpeed = 10f;
    public float reboundForceMultiplier = 1.2f;

    public bool JustRebounded { get; private set; }
    public bool CanRebound { get; private set; }

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        CanRebound = false;

        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        float speed = currentXZVelocity.magnitude;
        
        // 공중이거나 스피드가 일정 이상일 때만 벽 차기 가능 (정지 상태 차단)
        if (speed < 6f && _hub.movement.IsGrounded) return;

        Vector3 forward = _hub.transform.forward;
        // 상체 근처에서 벽 감지 (발 위치 오류 방지)
        Vector3 origin = _hub.transform.position + Vector3.up * 0.5f;

        // 정면의 벽 레이캐스트 감지
        if (Physics.Raycast(origin, forward, out RaycastHit wallHit, wallCheckDistance, _hub.wallRunner.wallLayerMask))
        {
            CanRebound = true;
        }
    }

    public void HandleJump()
    {
        JustRebounded = false;
        
        if (!CanRebound) return;

        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        float speed = currentXZVelocity.magnitude;
        
        Vector3 forward = _hub.transform.forward;
        Vector3 origin = _hub.transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(origin, forward, out RaycastHit wallHit, wallCheckDistance, _hub.wallRunner.wallLayerMask))
        {
            JustRebounded = true;

            // 1. 유효 스칼라 가속 산출 (최소속도 보정)
            float reboundSpd = Mathf.Max(speed * reboundForceMultiplier, minReboundSpeed);

            // 2. 물리적 입사/반사각 도출 (당구공)
            Vector3 reflectionDir = Vector3.Reflect(forward, wallHit.normal).normalized;
            reflectionDir.y = 0f; // 오로지 수평 방향의 튕겨나감만 취함
            reflectionDir = reflectionDir.normalized;

            // 3. 마우스 시선의 Snap-back 현상을 막도록 Yaw와 Pitch 강제 동기화
            _hub.movement.ForceLookDirection(reflectionDir);

            // 4. 점프력을 위쪽(Up)이 아닌 벽의 반대쪽(Reflection)으로 80~90% 이상 쏟아부어 완전한 탈출기 연출
            Vector3 impulse = (reflectionDir * 0.9f + Vector3.up * 0.1f).normalized * reboundSpd;
            _hub.Rb.linearVelocity = new Vector3(0f, _hub.Rb.linearVelocity.y, 0f); // XZ 관성 초기화
            _hub.Rb.AddForce(impulse, ForceMode.Impulse);

            // 5. 시각 효과 (벽을 차고 등을 돌리는 충격 하강)
            if (_hub.juiceController != null)
            {
                _hub.juiceController.TriggerLandingDrop(1.5f); // 내리찍는 Dip 빌려쓰기
            }
        }
    }
}
