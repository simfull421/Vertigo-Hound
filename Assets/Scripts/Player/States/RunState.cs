// ============================================================================
// RunState.cs — 이족보행 질주 상태 (기본 상태)
// ★ 점프/슬라이드: 푸시 방식 (HandleJump/HandleSlide)으로 즉시 실행
//   콜백 → Facade.RequestJump() → StateMachine.HandleJump() → RunState.HandleJump()
//   → Motor.Jump() + 즉시 ChangeState(Airborne)
// ★ Tick에서는 이동/카메라/낙하전이만 처리 (점프 폴링 제거)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class RunState : IState
    {
        private readonly CharacterFacade ctx;

        public RunState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            ctx.Animation?.PlayBipedalRun();
            ctx.Camera?.ResetTilt();
            ctx.Camera?.ResetHeight();
            ctx.Camera?.ResetShake();
        }

        // ══════════════════════════════════════════
        // ★ 푸시 방식 점프 — 콜백에서 즉시 호출됨
        // 파이프라인: OnJump 콜백 → Facade → StateMachine → 여기
        // ══════════════════════════════════════════

        public void HandleJump()
        {
            if (ctx.Motor.IsGrounded)
            {
                Debug.Log("[RunState] HandleJump — 점프 성공! Motor.Jump() + Airborne 즉시 전환");
                ctx.Motor.Jump();
                ctx.BroadcastAction("Jump");
                ctx.StateMachine.ChangeState(StateId.Airborne);
            }
            else
            {
                Debug.LogWarning($"[RunState] HandleJump — 점프 실패! IsGrounded=false, " +
                                 $"velocity.y={ctx.Motor.Velocity.y:F2}");
            }
        }

        // ══════════════════════════════════════════
        // ★ 푸시 방식 슬라이드 — 콜백에서 즉시 호출됨
        // ══════════════════════════════════════════

        public void HandleSlide()
        {
            if (ctx.Motor.IsGrounded && ctx.Motor.CurrentSpeed > 1f)
            {
                Debug.Log("[RunState] HandleSlide — 슬라이드 전환");
                ctx.StateMachine.ChangeState(StateId.Slide);
            }
        }

        // ══════════════════════════════════════════
        // Tick — 이동, 카메라 연출, 낙하/벽 전이만 처리
        // ★ 점프/슬라이드 폴링은 여기서 완전히 제거됨
        // ══════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // ── 전이 조건 (점프/슬라이드 외) ──

            // 벽 감지 + Grab + 공중 → WallRunState
            if (ctx.Motor.IsTouchingWall && ctx.Input.IsGrabbing && !ctx.Motor.IsGrounded)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.StateMachine.ChangeState(StateId.WallRun);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }

            // 바닥에서 벗어남 (절벽 낙하) → AirborneState
            if (!ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ── 이동 ──
            ctx.Motor.Move(ctx.CurrentMoveDirection);

            // ── 카메라 연출 ──
            float speedRatio = Mathf.Clamp01(ctx.Motor.CurrentSpeed / ctx.Motor.MaxSprintSpeed);
            ctx.Camera?.ApplySpeedFOV(speedRatio);

            if (speedRatio > 0.3f)
                ctx.Camera?.ApplyShake(speedRatio * 0.4f);
            else
                ctx.Camera?.ResetShake();

            ctx.Animation?.SetSpeed(ctx.Motor.CurrentSpeed);
        }

        public void Exit()
        {
            ctx.Camera?.ResetFOV();
            ctx.Camera?.ResetShake();
        }
    }
}
