// ============================================================================
// RunState.cs — 이족보행 질주 상태 (기본 상태)
// 게임 시작 시 초기 상태. W키로 전진, 속도에 비례한 셰이크/FOV 적용.
// 전이: → AirborneState(점프), → SlideState(C/Ctrl),
//       → FreeFallState(낙하 트리거), → WallRunState(벽+Grab)
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
            ctx.Animation.PlayBipedalRun();
            ctx.Camera.ResetTilt();
            ctx.Camera.ResetHeight();
        }

        public void Tick(float deltaTime)
        {
            // ── 이동 ──
            ctx.Motor.Move(ctx.CurrentMoveDirection);

            // ── 속도 비례 카메라 연출 ──
            float speedRatio = ctx.Motor.CurrentSpeed / 20f; // maxSprintSpeed 기준
            ctx.Camera.ApplySpeedFOV(speedRatio);
            ctx.Camera.ApplyShake(speedRatio * 0.3f);
            ctx.Animation.SetSpeed(ctx.Motor.CurrentSpeed);

            // ── 전이 조건 검사 ──

            // 점프 → AirborneState
            if (ctx.Input.JumpPressed && ctx.Motor.IsGrounded)
            {
                ctx.Motor.Jump();
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // 슬라이드 → SlideState
            if (ctx.Input.SlidePressed && ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Slide);
                return;
            }

            // 벽 감지 + Grab 유지 → WallRunState (방식 B)
            if (ctx.Motor.IsTouchingWall && ctx.Input.IsGrabbing && !ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.WallRun);
                return;
            }

            // 바닥에서 떨어짐 (점프 없이 절벽 낙하) → AirborneState
            if (!ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }
        }

        public void Exit()
        {
            ctx.Camera.ResetFOV();
        }
    }
}
