// ============================================================================
// WallRunState.cs — 나선형 벽 타기 상태
// ★ Enter에서 Motor.EnableCustomGravity = false → 기본 중력(25f) 차단
//   → wallRunDownforce(3f)만으로 부드러운 나선형 하강
// ★ Exit에서 Motor.EnableCustomGravity = true → 기본 중력 복구
// ★ 틸트: 왼쪽 벽 = Z축 음수(시계=오른쪽), 오른쪽 벽 = Z축 양수(반시계=왼쪽)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class WallRunState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>
        /// 틸트 방향.
        /// -1 = 왼쪽 벽 → Z축 음수 (오른쪽 기울임)
        /// +1 = 오른쪽 벽 → Z축 양수 (왼쪽 기울임)
        /// </summary>
        private int tiltDirection;

        public WallRunState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            // ── 벽 법선으로 틸트 방향 계산 ──
            Vector3 wallNormal = ctx.Motor.LastWallNormal;
            float dot = Vector3.Dot(ctx.Motor.transform.right, wallNormal);

            // dot > 0 → 법선이 오른쪽 → 왼쪽 벽 → Z- (오른쪽 기울임) → direction=-1
            // dot < 0 → 법선이 왼쪽  → 오른쪽 벽 → Z+ (왼쪽 기울임) → direction=+1
            tiltDirection = dot > 0 ? -1 : 1;

            Debug.Log($"[WallRunState] Enter — wallNormal={wallNormal}, " +
                      $"dot={dot:F2}, tiltDirection={tiltDirection} " +
                      $"({(tiltDirection < 0 ? "오른쪽 기울임 (왼쪽 벽)" : "왼쪽 기울임 (오른쪽 벽)")})");

            // ★ 기본 중력 차단 — wallRunDownforce만 적용되도록
            ctx.Motor.EnableCustomGravity = false;

            // Y축 속도 제거 (벽에 부착 — 수평 Momentum만 보존)
            ctx.Motor.ResetVerticalVelocity();

            // 카메라 틸트
            ctx.Camera?.ApplyWallRunTilt(tiltDirection);

            ctx.Animation?.PlayWallRun();
            ctx.BroadcastAction("WallRunStart");
        }

        // ══════════════════════════════════════════
        // ★ 푸시 방식 점프 — 콜백에서 즉시 호출됨
        //   벽 차기(Wall Jump): 스페이스바
        // ══════════════════════════════════════════

        public void HandleJump()
        {
            Debug.Log("[WallRunState] HandleJump — Wall Jump 실행!");
            ctx.Motor.WallJump();
            ctx.BroadcastAction("WallJump");
            ctx.StateMachine.ChangeState(StateId.Airborne);
        }

        public void Tick(float deltaTime)
        {
            // ══════════════════════════════════════════
            // 1. Grab 해제 → 즉시 떨어짐 (방식 B)
            // ══════════════════════════════════════════
            if (!ctx.Input.IsGrabbing)
            {
                Debug.Log("[WallRunState] Grab 해제 → AirborneState");
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ══════════════════════════════════════════
            // 2. 벽에서 벗어남 → 떨어짐
            // ══════════════════════════════════════════
            if (!ctx.Motor.IsTouchingWall)
            {
                Debug.Log("[WallRunState] 벽 이탈 → AirborneState");
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ══════════════════════════════════════════
            // 3. 나선형 하강(Spiral Descent) 물리
            //    ★ 기본 중력(25f)은 꺼져 있으므로, wallRunDownforce(3f)만 작용
            //    → 부드러운 나선형 미끄러짐
            // ══════════════════════════════════════════
            ctx.Motor.ApplyWallRunDownforce();

            // 전진 방향으로 이동 유지 (벽 면을 따라)
            Vector3 wallForward = ctx.Motor.transform.forward;
            ctx.Motor.Move(wallForward);

            // ══════════════════════════════════════════
            // 4. 착지 → RunState
            // ══════════════════════════════════════════
            if (ctx.Motor.IsGrounded)
            {
                Debug.Log("[WallRunState] 착지 → RunState");
                ctx.StateMachine.ChangeState(StateId.Run);
                return;
            }
        }

        public void Exit()
        {
            // ★ 기본 중력 복구 — 벽에서 나가면 다시 정상 중력 적용
            ctx.Motor.EnableCustomGravity = true;

            ctx.Camera?.ResetTilt();

            Debug.Log("[WallRunState] Exit — EnableCustomGravity=true 복구");
        }
    }
}
