// ============================================================================
// IState.cs — FSM 상태 인터페이스
// ★ 입력 명령을 즉시 전달받는 HandleXxx() 메서드 추가
//   기본 구현(빈 메서드)을 제공하여, 해당 입력에 반응하지 않는 상태는
//   별도 구현 없이 무시합니다.
// ============================================================================

namespace VertigoHound.Player
{
    /// <summary>
    /// FSM 상태 인터페이스. 모든 상태 클래스는 이것을 구현합니다.
    /// Enter → Tick(매 프레임) → Exit 생명주기를 따릅니다.
    /// ★ HandleJump/HandleSlide는 콜백에서 즉시 호출되는 푸시 방식 명령입니다.
    /// </summary>
    public interface IState
    {
        /// <summary>이 상태에 진입할 때 단 한 번 호출됩니다.</summary>
        void Enter();

        /// <summary>이 상태가 활성 상태인 동안 매 프레임 호출됩니다.</summary>
        void Tick(float deltaTime);

        /// <summary>이 상태에서 빠져나갈 때 단 한 번 호출됩니다.</summary>
        void Exit();

        // ──────────────────────────────────────────────
        // 즉시 명령 (Push-based Input Commands)
        // 콜백 → Facade → StateMachine → 현재 State로 즉시 전달됩니다.
        // 기본 구현은 빈 메서드 — 반응하지 않는 상태는 무시합니다.
        // ──────────────────────────────────────────────

        /// <summary>
        /// 점프 명령. 콜백에서 즉시 호출됩니다.
        /// RunState: Motor.Jump() + Airborne 전환.
        /// CeilingState: DetachFromCeiling + Airborne 전환.
        /// 기본: 무시.
        /// </summary>
        void HandleJump() { }

        /// <summary>
        /// 슬라이드 명령. 콜백에서 즉시 호출됩니다.
        /// RunState: Slide 전환.
        /// 기본: 무시.
        /// </summary>
        void HandleSlide() { }
    }
}
