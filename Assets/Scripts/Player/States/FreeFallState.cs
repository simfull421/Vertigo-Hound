// ============================================================================
// FreeFallState.cs — 수직 자유낙하 상태 (번지점프)
// 낙하 트리거 진입 후 3초 동안 자유낙하. 체공 시간 경과 시 바닥 자동 생성 후
// QuadRecoveryState로 강제 전환됩니다.
// ★ AirborneState와 다름: 이 상태는 전용 연출(백덤블링, 모션블러)이 있고
//   시간 기반으로 자동 종료됩니다.
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.Player.States
{
    public sealed class FreeFallState : IState
    {
        private readonly CharacterFacade ctx;

        /// <summary>자유낙하 최대 시간(초). 이 시간이 지나면 바닥 생성 → QuadRecovery 전환</summary>
        private const float FallDuration = 3.0f;

        /// <summary>공중 좌우 회피(Air Strafing) 조작력</summary>
        private const float AirStrafeForce = 6f;

        private float elapsedTime;

        /// <summary>바닥 생성 요청 이벤트. MapSystem이 구독하여 바닥을 생성합니다.</summary>
        public static event Action OnFloorSpawnRequested;

        public FreeFallState(CharacterFacade context)
        {
            ctx = context;
        }

        public void Enter()
        {
            elapsedTime = 0f;

            // 카메라 연출: 180도 백덤블링
            ctx.Camera.PlayBackflipSequence();

            // Y축 속도 리셋 후 중력으로 가속 (자연스러운 낙하 시작)
            ctx.Motor.ResetVerticalVelocity();

            ctx.Animation.PlayAirborne();
            ctx.BroadcastAction("FreeFallEnter");
        }

        public void Tick(float deltaTime)
        {
            elapsedTime += deltaTime;

            // ── Air Strafing (공중 좌우 회피) ──
            if (ctx.CurrentMoveDirection.sqrMagnitude > 0.01f)
            {
                ctx.Motor.AirStrafe(ctx.CurrentMoveDirection, AirStrafeForce);
            }

            // ── 시간 경과 체크: 3초 후 자동 전환 ──
            if (elapsedTime >= FallDuration)
            {
                // 1. 바닥 생성 요청 (MapSystem에 이벤트 방송)
                OnFloorSpawnRequested?.Invoke();

                // 2. QuadRecoveryState로 강제 전환
                ctx.StateMachine.ChangeState(StateId.QuadRecovery);
            }
        }

        public void Exit()
        {
            // 백덤블링 정지, 모션블러 해제 등은 여기서 처리
        }
    }
}
