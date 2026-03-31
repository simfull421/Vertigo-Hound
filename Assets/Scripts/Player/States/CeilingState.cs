// ============================================================================
// CeilingState.cs — 천장 달리기 (중력 반전) 상태
// ★ Step 3: 핵심 로직 구현 완료
// 월드 중력을 180도 반전하여 천장을 바닥처럼 사용합니다.
// ★ 방식 B: Grab 유지 시에만 천장에 붙어있음. 놓으면 즉시 낙하.
// ★ 중력 반전 = PhysicsMotor.InvertGravity() (커스텀 중력 방향 전환)
//   + CameraController.PlaySnapInvert() (시각적 180도 스냅턴)
// ★ 입력 이벤트를 직접 듣지 않음 — ctx.Input의 상태값만 읽음
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class CeilingState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>천장 달리기 진입 시 보존된 Momentum</summary>
        private float entrySpeed;

        public CeilingState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            entrySpeed = ctx.Motor.CurrentSpeed;

            // ══════════════════════════════════════════
            // 1. 물리: 중력 반전 (커스텀 중력 방향을 위로)
            // ══════════════════════════════════════════
            ctx.Motor.InvertGravity();

            // ══════════════════════════════════════════
            // 2. 카메라: 0.05~0.1초 만에 180도 스냅턴
            //    시각적으로 천장이 바닥이 되는 연출
            // ══════════════════════════════════════════
            ctx.Camera?.PlaySnapInvert();

            // ══════════════════════════════════════════
            // 3. 애니메이션: 이족보행 (천장 위에서도 달리기)
            // ══════════════════════════════════════════
            ctx.Animation?.PlayBipedalRun();

            // ══════════════════════════════════════════
            // 4. 이벤트 방송 (콤보 배수 증가 시작)
            // ══════════════════════════════════════════
            ctx.BroadcastAction("CeilingGrab");

            // Momentum 보존
            ctx.Motor.PreserveMomentum(entrySpeed);
        }

        // ══════════════════════════════════════════
        // ★ 푸시 방식 점프 — 콜백에서 즉시 호출됨
        //   천장 → 중력 복구 + 점프 + AirborneState
        // ══════════════════════════════════════════

        public void HandleJump()
        {
            Debug.Log("[CeilingState] HandleJump — 천장 탈출! DetachFromCeiling + Jump + Airborne");
            DetachFromCeiling();
            ctx.Motor.Jump();
            ctx.StateMachine.ChangeState(StateId.Airborne);
        }

        public void Tick(float deltaTime)
        {
            // ══════════════════════════════════════════
            // 1. 이동 — 반전된 중력 하에서 천장 위를 달림
            // ══════════════════════════════════════════
            ctx.Motor.Move(ctx.CurrentMoveDirection);

            float speedRatio = Mathf.Clamp01(ctx.Motor.CurrentSpeed / ctx.Motor.MaxSprintSpeed);
            ctx.Camera?.ApplySpeedFOV(speedRatio);
            ctx.Animation?.SetSpeed(ctx.Motor.CurrentSpeed);

            // ══════════════════════════════════════════
            // 2. 전이 조건 (점프는 HandleJump에서 처리 — 여기서 폴링하지 않음)
            // ══════════════════════════════════════════

            // Grab 해제 → 즉시 중력 복구 + 낙하 (방식 B)
            if (!ctx.Input.IsGrabbing)
            {
                DetachFromCeiling();
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // 벽 감지 + Grab → 천장에서 벽으로 연계
            if (ctx.Motor.IsTouchingWall)
            {
                DetachFromCeiling();
                ctx.StateMachine.ChangeState(StateId.WallRun);
                return;
            }
        }

        public void Exit()
        {
            // 스냅턴 시각 복귀 (카메라를 180도 되돌림)
            ctx.Camera?.PlaySnapRestore();
        }

        // ──────────────────────────────────────────────
        // 내부 헬퍼: 천장에서 떨어질 때 공통 로직
        // ──────────────────────────────────────────────

        /// <summary>
        /// 중력 복구 + Momentum 보존.
        /// 모든 Exit 경로에서 공통으로 호출됩니다.
        /// </summary>
        private void DetachFromCeiling()
        {
            float preservedSpeed = ctx.Motor.CurrentSpeed;
            ctx.Motor.RestoreGravity();
            ctx.Motor.PreserveMomentum(preservedSpeed);
        }
    }
}
