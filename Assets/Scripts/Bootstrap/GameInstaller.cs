// ============================================================================
// GameInstaller.cs — 중앙집권 부트스트래퍼 (coding.md 철칙)
// 게임 시작 시 모든 시스템을 생성하고, 참조를 주입하며, 이벤트를 연결합니다.
// ★ 이 클래스가 "유일한 초기화 지점"입니다.
// ★ 씬에 단 하나만 존재해야 합니다.
// ============================================================================

using UnityEngine;
using VertigoHound.Player;
using VertigoHound.Map;
using VertigoHound.UI;
using VertigoHound.Tutorial;

namespace VertigoHound.Bootstrap
{
    public class GameInstaller : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 참조 (씬에 배치된 매니저/시스템들)
        // ──────────────────────────────────────────────

        [Header("=== Player System ===")]
        [Tooltip("플레이어 루트 오브젝트 (CharacterFacade가 붙어야 함)")]
        [SerializeField] private CharacterFacade characterFacade;

        [Header("=== Map System ===")]
        [Tooltip("맵 패턴 매니저")]
        [SerializeField] private MapPatternManager mapPatternManager;

        [Tooltip("청크 풀러")]
        [SerializeField] private ChunkPooler chunkPooler;

        [Tooltip("Void 벽 (추격 레이저)")]
        [SerializeField] private VoidWall voidWall;

        [Header("=== UI System ===")]
        [Tooltip("UI 매니저")]
        [SerializeField] private UIManager uiManager;

        [Tooltip("점수 매니저")]
        [SerializeField] private ScoreManager scoreManager;

        [Header("=== Tutorial System ===")]
        [Tooltip("튜토리얼 매니저")]
        [SerializeField] private TutorialManager tutorialManager;

        // ──────────────────────────────────────────────
        // 초기화 (게임 시작 시 단 한 번)
        // ──────────────────────────────────────────────

        private void Awake()
        {
            // 프레임 레이트 설정 (성능 일관성)
            Application.targetFrameRate = 144;

            InstallDependencies();
            WireEvents();
        }

        /// <summary>
        /// 모든 시스템에 필요한 참조(DI)를 주입합니다.
        /// </summary>
        private void InstallDependencies()
        {
            Transform playerTransform = characterFacade.transform;

            // Map System → 플레이어 참조 주입
            if (chunkPooler != null)
                chunkPooler.Initialize(playerTransform);

            if (voidWall != null)
                voidWall.Initialize(playerTransform);

            // Score System → Facade 이벤트 구독
            if (scoreManager != null)
                scoreManager.Initialize(characterFacade);

            // Tutorial
            if (tutorialManager != null)
                tutorialManager.Initialize();
        }

        /// <summary>
        /// 시스템 간 이벤트를 연결합니다. (느슨한 결합)
        /// 모든 이벤트 구독은 이 한 곳에서만 수행합니다.
        /// </summary>
        private void WireEvents()
        {
            // Player → UI: 상태 변경 알림
            if (characterFacade != null && uiManager != null)
            {
                characterFacade.OnStateChanged += uiManager.UpdateState;
            }

            // Player → UI: 콤보 배수 변경
            if (characterFacade != null && uiManager != null)
            {
                characterFacade.OnComboUpdate += (combo) => uiManager.UpdateCombo(combo);
            }

            // Score → UI: 점수 업데이트
            if (scoreManager != null && uiManager != null)
            {
                scoreManager.OnScoreUpdate += uiManager.UpdateScore;
                scoreManager.OnComboUpdate += uiManager.UpdateCombo;
            }

            // Void Wall → UI: 게임 오버
            if (voidWall != null && uiManager != null)
            {
                voidWall.OnVoidCaught += uiManager.ShowGameOver;
            }

            // Map Pattern → Chunk Pooler: 청크 생성 요청
            if (mapPatternManager != null && chunkPooler != null)
            {
                mapPatternManager.OnChunkRequested += (prefab, pos) => chunkPooler.SpawnChunk(pos);
            }
        }

        // ──────────────────────────────────────────────
        // 정리
        // ──────────────────────────────────────────────

        private void OnDestroy()
        {
            // 이벤트 구독 해제 (메모리 누수 방지)
            if (characterFacade != null && uiManager != null)
            {
                characterFacade.OnStateChanged -= uiManager.UpdateState;
            }

            if (scoreManager != null && uiManager != null)
            {
                scoreManager.OnScoreUpdate -= uiManager.UpdateScore;
            }

            if (voidWall != null && uiManager != null)
            {
                voidWall.OnVoidCaught -= uiManager.ShowGameOver;
            }
        }
    }
}
