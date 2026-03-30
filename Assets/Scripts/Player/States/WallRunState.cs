// ============================================================================
// WallRunState.cs — 나선형 벽 타기 상태
// 벽에 달라붙어 약한 Downforce를 받으며 나선형으로 하강합니다.
// ★ 방식 B: Grab 버튼(우클릭/Shift)을 유지해야 벽에 붙어있음.
//   Grab을 놓으면 즉시 떨어짐.
// 전이: → AirborneState(벽 차기/Grab 해제),
//       → CeilingState(벽 차기로 천장 도달 + Grab)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class WallRunState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>벽에 부착된 방향. 1 = 우측 벽, -1 = 좌측 벽</summary>
        private int wallDirection;

        public WallRunState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            // 벽 법선으로 방향 판별 (법선이 왼쪽을 가리키면 우측 벽에 붙어있음)
            Vector3 wallNormal = ctx.Motor.LastWallNormal;
            float dot = Vector3.Dot(ctx.Motor.transform.right, wallNormal);
            wallDirection = dot < 0 ? 1 : -1;

            // Y축 속도 제거 (벽에 부착)
            ctx.Motor.ResetVerticalVelocity();

            // 카메라 연출: 벽 방향으로 Tilt
            ctx.Camera.ApplyWallRunTilt(wallDirection);

            ctx.Animation.PlayWallRun();
            ctx.BroadcastAction("WallRunStart");
        }

        public void Tick(float deltaTime)
        {
            // ── Grab 해제 → 즉시 떨어짐 (방식 B) ──
            if (!ctx.Input.IsGrabbing)
            {
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ── 벽에서 벗어남 → 떨어짐 ──
            if (!ctx.Motor.IsTouchingWall)
            {
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ── 나선형 하강: 약한 Downforce 적용 ──
            ctx.Motor.ApplyWallRunDownforce();

            // 전진 속도 유지 (Momentum 보존)
            ctx.Motor.Move(ctx.Motor.transform.forward * ctx.Motor.CurrentSpeed);

            // ── 벽 차기(Wall Jump): 스페이스바 ──
            if (ctx.Input.JumpPressed)
            {
                // ★ 핵심: 법선 벡터 기반 대각선 Impulse + Momentum 100% 보존
                ctx.Motor.WallJump();
                ctx.BroadcastAction("WallJump");
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }

            // ── 착지 → RunState ──
            if (ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Run);
                return;
            }
        }

        public void Exit()
        {
            // 카메라 Tilt 복구
            ctx.Camera.ResetTilt();
        }
    }
}
