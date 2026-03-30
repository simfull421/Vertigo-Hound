// ============================================================================
// AirborneState.cs — 공중 체공 상태 (일반 공중)
// ★ 신규 추가: 점프, 벽 차기 후 공중에 있는 동안의 상태.
// FreeFallState(번지 3초)와 구분됩니다.
// 전이: → RunState(착지), → WallRunState(벽+Grab),
//       → CeilingState(천장+Grab), → FreeFallState(낙하 트리거)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class AirborneState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>공중 좌우 조작력</summary>
        private const float AirStrafeForce = 8f;

        public AirborneState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            ctx.Animation.PlayAirborne();
        }

        public void Tick(float deltaTime)
        {
            // ── Air Strafing (공중 좌우 미세 조작) ──
            if (ctx.CurrentMoveDirection.sqrMagnitude > 0.01f)
            {
                ctx.Motor.AirStrafe(ctx.CurrentMoveDirection, AirStrafeForce);
            }

            // ── 전이 조건 검사 ──

            // 착지 → RunState
            if (ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Run);
                return;
            }

            // 벽 감지 + Grab 유지 → WallRunState (방식 B)
            if (ctx.Motor.IsTouchingWall && ctx.Input.IsGrabbing)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.StateMachine.ChangeState(StateId.WallRun);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }

            // 천장 감지 + Grab 유지 → CeilingState (방식 B)
            if (ctx.Motor.IsTouchingCeiling && ctx.Input.IsGrabbing)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.StateMachine.ChangeState(StateId.Ceiling);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }
        }

        public void Exit()
        {
            // 공중에서 나갈 때 특별한 정리 없음
        }
    }
}
