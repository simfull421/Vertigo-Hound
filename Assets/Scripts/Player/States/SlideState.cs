// ============================================================================
// SlideState.cs — 슬라이딩 상태
// 질주 중 C/Ctrl 입력 시 진입. 충돌체 높이 반감, 마찰력 0.
// FOV 순간 팽창, 카메라 높이 낮아짐.
// 전이: → RunState(키 해제/슬라이드 종료)
//       → AirborneState(슬라이딩 중 바닥 없음)
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class SlideState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>슬라이딩 최소 지속 시간(초). 너무 짧은 슬라이딩 방지</summary>
        private const float MinSlideDuration = 0.3f;

        /// <summary>슬라이딩 최대 지속 시간(초). 무제한 슬라이딩 방지</summary>
        private const float MaxSlideDuration = 1.5f;

        /// <summary>슬라이딩 시 카메라 높이</summary>
        private const float SlideCameraHeight = 0.5f;

        private float elapsedTime;
        private float storedSpeed;

        public SlideState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            elapsedTime = 0f;

            // 진입 시 속도 저장 (슬라이딩 동안 이 속도로 활주)
            storedSpeed = ctx.Motor.CurrentSpeed;

            // 충돌체 높이 반감
            ctx.Motor.ShrinkCollider();

            // 마찰력 제거
            ctx.Motor.SetFriction(0f);

            // 카메라 연출: FOV 팽창 + 높이 낮춤
            ctx.Camera?.ApplySlideFOV();
            ctx.Camera?.SetHeight(SlideCameraHeight);

            ctx.Animation?.PlaySlide();
        }

        public void Tick(float deltaTime)
        {
            elapsedTime += deltaTime;

            // ── 활주: 마찰 없이 전진 방향으로 미끄러짐 ──
            Vector3 slideDirection = ctx.Motor.transform.forward;
            ctx.Motor.SetVelocity(new Vector3(
                slideDirection.x * storedSpeed,
                ctx.Motor.Velocity.y,
                slideDirection.z * storedSpeed
            ));

            // ── 전이 조건 검사 ──

            // 키 해제 + 최소 시간 경과 → RunState
            if (!ctx.Input.SlideHeld && elapsedTime >= MinSlideDuration)
            {
                ctx.StateMachine.ChangeState(StateId.Run);
                return;
            }

            // 최대 시간 초과 → RunState
            if (elapsedTime >= MaxSlideDuration)
            {
                ctx.StateMachine.ChangeState(StateId.Run);
                return;
            }

            // 바닥에서 벗어남 → AirborneState
            if (!ctx.Motor.IsGrounded)
            {
                ctx.StateMachine.ChangeState(StateId.Airborne);
                return;
            }
        }

        public void Exit()
        {
            // 충돌체 복구
            ctx.Motor.RestoreCollider();

            // 마찰력 복구 (기본값)
            ctx.Motor.SetFriction(0.6f);

            // 카메라 복구
            ctx.Camera?.ResetFOV();
            ctx.Camera?.ResetHeight();
        }
    }
}
