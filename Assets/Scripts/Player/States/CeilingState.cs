// ============================================================================
// CeilingState.cs — 천장 달리기 (중력 반전) 상태
// 월드 중력을 180도 반전하여 천장을 바닥처럼 사용합니다.
// ★ 방식 B: 천장에 닿았을 때 Grab 유지 시에만 진입.
// 전이: → AirborneState(스페이스바 → 중력 복구 → 낙하)
//       → WallRunState(천장에서 벽으로 연계 + Grab)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class CeilingState : IState
    {
        private readonly CharacterFacade ctx;

        public CeilingState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            // ★ 핵심: 중력 180도 반전 (천장 = 바닥)
            ctx.Motor.InvertGravity();

            // 카메라 연출: 0.05~0.1초 만에 180도 스냅턴
            ctx.Camera.PlaySnapInvert();

            ctx.Animation.PlayBipedalRun();
            ctx.BroadcastAction("CeilingGrab");
        }

        public void Tick(float deltaTime)
        {
            // ── 천장 위에서 질주 (반전된 중력으로 바닥처럼 이동) ──
            ctx.Motor.Move(ctx.CurrentMoveDirection);
            ctx.Animation.SetSpeed(ctx.Motor.CurrentSpeed);

            // ── 스페이스바 → 중력 복구, AirborneState로 낙하 ──
            if (ctx.Input.JumpPressed)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.Motor.RestoreGravity();
                ctx.StateMachine.ChangeState(StateId.Airborne);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }

            // ── Grab 해제 → 즉시 낙하 (방식 B) ──
            if (!ctx.Input.IsGrabbing)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.Motor.RestoreGravity();
                ctx.StateMachine.ChangeState(StateId.Airborne);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }

            // ── 벽 감지 + Grab → WallRunState로 연계 ──
            if (ctx.Motor.IsTouchingWall && ctx.Input.IsGrabbing)
            {
                float preservedSpeed = ctx.Motor.CurrentSpeed;
                ctx.Motor.RestoreGravity();
                ctx.StateMachine.ChangeState(StateId.WallRun);
                ctx.Motor.PreserveMomentum(preservedSpeed);
                return;
            }
        }

        public void Exit()
        {
            // 카메라 스냅턴 복귀 (이미 RestoreGravity에서 축 복구되지만, 시각 연출은 별도)
            ctx.Camera.PlaySnapRestore();
        }
    }
}
