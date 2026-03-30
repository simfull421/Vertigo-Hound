// ============================================================================
// StateId.cs — FSM 상태 식별 열거형
// CharacterStateMachine의 Dictionary 키와 이벤트 페이로드로 사용됩니다.
// ============================================================================

namespace VertigoHound.Player
{
    /// <summary>
    /// 플레이어 캐릭터의 모든 이동 상태를 식별하는 열거형입니다.
    /// </summary>
    public enum StateId
    {
        /// <summary>이족 보행 질주 (기본 상태)</summary>
        Run,

        /// <summary>공중 체공 — 점프, 벽 차기 후 체공 (일반 공중)</summary>
        Airborne,

        /// <summary>수직 자유낙하 — 번지 트리거 진입 (3초 타이머)</summary>
        FreeFall,

        /// <summary>사족보행 착지 회복 — 낙하 후 1.5초 회복</summary>
        QuadRecovery,

        /// <summary>나선형 벽 타기 — 벽에 부착되어 나선 하강</summary>
        WallRun,

        /// <summary>천장 달리기 — 중력 반전 상태</summary>
        Ceiling,

        /// <summary>슬라이딩 — 충돌체 축소, 마찰 0</summary>
        Slide
    }
}
