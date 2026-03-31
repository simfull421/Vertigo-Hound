// ============================================================================
// CharacterStateMachine.cs — FSM 상태 전환 엔진
// IState 기반 상태 관리. 상태 등록, 전환, Tick 루프를 담당합니다.
// ★ 이 클래스는 CharacterFacade에 의해 소유되며, 외부에서 직접 접근하지 않습니다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VertigoHound.Player
{
    public class CharacterStateMachine
    {
        // ──────────────────────────────────────────────
        // 이벤트 (느슨한 결합 — coding.md 원칙)
        // ──────────────────────────────────────────────

        /// <summary>상태가 전환될 때 방송. UI, 콤보 시스템 등이 구독합니다.</summary>
        public event Action<StateId> OnTransition;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private readonly Dictionary<StateId, IState> states = new Dictionary<StateId, IState>();
        private IState currentState;
        private StateId currentStateId;

        // ──────────────────────────────────────────────
        // 읽기 전용 프로퍼티
        // ──────────────────────────────────────────────

        /// <summary>현재 활성 상태의 ID</summary>
        public StateId CurrentStateId => currentStateId;

        /// <summary>현재 활성 상태 객체</summary>
        public IState CurrentState => currentState;

        // ──────────────────────────────────────────────
        // 상태 등록
        // ──────────────────────────────────────────────

        /// <summary>
        /// 상태를 FSM에 등록합니다. GameInstaller 또는 CharacterFacade의
        /// 초기화 시점에서 모든 상태를 등록합니다.
        /// </summary>
        public void RegisterState(StateId id, IState state)
        {
            if (states.ContainsKey(id))
            {
                Debug.LogWarning($"[FSM] 상태 '{id}'가 이미 등록되어 있어 덮어씁니다.");
            }
            states[id] = state;
        }

        // ──────────────────────────────────────────────
        // 상태 전환
        // ──────────────────────────────────────────────

        /// <summary>
        /// 다른 상태로 전환합니다.
        /// 현재 상태의 Exit() → 새 상태의 Enter() 순서로 호출됩니다.
        /// </summary>
        public void ChangeState(StateId newStateId)
        {
            if (!states.ContainsKey(newStateId))
            {
                Debug.LogError($"[FSM] 등록되지 않은 상태로 전환 시도: {newStateId}");
                return;
            }

            // 현재 상태가 같으면 무시
            if (currentState != null && currentStateId == newStateId) return;

            // Exit → Enter
            currentState?.Exit();

            currentStateId = newStateId;
            currentState = states[newStateId];
            currentState.Enter();

            // 전환 이벤트 방송
            OnTransition?.Invoke(newStateId);
        }

        // ──────────────────────────────────────────────
        // Tick (매 프레임 호출)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 활성 상태의 Tick을 호출합니다.
        /// CharacterFacade의 Update에서 매 프레임 호출합니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            currentState?.Tick(deltaTime);
        }

        // ──────────────────────────────────────────────
        // 즉시 명령 라우터 (Push-based)
        // 콜백 → Facade → 여기 → 현재 State로 전달
        // ──────────────────────────────────────────────

        /// <summary>점프 명령을 현재 활성 상태에 즉시 전달합니다.</summary>
        public void HandleJump()
        {
            currentState?.HandleJump();
        }

        /// <summary>슬라이드 명령을 현재 활성 상태에 즉시 전달합니다.</summary>
        public void HandleSlide()
        {
            currentState?.HandleSlide();
        }

        // ──────────────────────────────────────────────
        // 초기 상태 설정
        // ──────────────────────────────────────────────

        /// <summary>
        /// 게임 시작 시 초기 상태를 설정합니다.
        /// ChangeState와 달리 Exit() 없이 바로 Enter()만 호출합니다.
        /// </summary>
        public void Initialize(StateId initialStateId)
        {
            if (!states.ContainsKey(initialStateId))
            {
                Debug.LogError($"[FSM] 초기 상태 '{initialStateId}'가 등록되지 않았습니다.");
                return;
            }

            currentStateId = initialStateId;
            currentState = states[initialStateId];
            currentState.Enter();
            OnTransition?.Invoke(initialStateId);
        }
    }
}
