// ============================================================================
// QuadRecoveryState.cs — 사족보행 착지 회복 상태
// 자유낙하(FreeFall) 후 착지 시 진입. Y축 추락 속도를 Z축 전진 속도로
// 100% 치환하여 멈춤 없이 질주합니다. 1.5초 후 이족보행(RunState)으로 복귀.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class QuadRecoveryState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>사족보행 유지 시간(초). 경과 후 RunState로 복귀</summary>
        private const float RecoveryDuration = 1.5f;

        private float elapsedTime;

        public QuadRecoveryState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            elapsedTime = 0f;

            // ★ 핵심: Y축 추락 속도 → Z축 전진 속도로 100% 전환
            ctx.Motor.ConvertVerticalToForward();

            // 카메라 높이를 사족보행 높이로 낮춤 (0.8~1m)
            ctx.Camera.SetQuadHeight();

            // 사족보행 애니메이션 재생
            ctx.Animation.PlayQuadRun();

            ctx.BroadcastAction("FreeFallLanded");
        }

        public void Tick(float deltaTime)
        {
            elapsedTime += deltaTime;

            // 사족보행 중에도 이동 유지 (Momentum 유지)
            ctx.Motor.Move(ctx.CurrentMoveDirection);
            ctx.Animation.SetSpeed(ctx.Motor.CurrentSpeed);

            // ── 1.5초 경과 → RunState 복귀 ──
            if (elapsedTime >= RecoveryDuration)
            {
                ctx.StateMachine.ChangeState(StateId.Run);
            }
        }

        public void Exit()
        {
            // 카메라 높이를 기본(1.7m)으로 부드럽게 복구
            ctx.Camera.ResetHeight();
        }
    }
}
