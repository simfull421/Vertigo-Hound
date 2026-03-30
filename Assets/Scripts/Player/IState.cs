// ============================================================================
// IState.cs — 모든 FSM 상태(State)의 계약서(인터페이스)
// 각 상태(RunState, CeilingState 등)는 이 세 메서드를 반드시 구현해야 합니다.
// ============================================================================

namespace VertigoHound.Player
{
    /// <summary>
    /// FSM 상태 인터페이스. 모든 상태 클래스는 이것을 구현합니다.
    /// Enter → Tick(매 프레임) → Exit 생명주기를 따릅니다.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 이 상태에 진입할 때 단 한 번 호출됩니다.
        /// 카메라 연출 시작, 물리 파라미터 설정 등 초기화를 수행합니다.
        /// </summary>
        void Enter();

        /// <summary>
        /// 이 상태가 활성 상태인 동안 매 프레임 호출됩니다.
        /// 물리 연산, 타이머 체크, 전이 조건 검사 등을 수행합니다.
        /// </summary>
        /// <param name="deltaTime">프레임 간 경과 시간 (Time.deltaTime)</param>
        void Tick(float deltaTime);

        /// <summary>
        /// 이 상태에서 빠져나갈 때 단 한 번 호출됩니다.
        /// 카메라 원복, 물리 파라미터 정리 등 정리(Cleanup)를 수행합니다.
        /// </summary>
        void Exit();
    }
}
