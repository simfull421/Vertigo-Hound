// ============================================================================
// TutorialManager.cs — 일회성 튜토리얼 시퀀스
// 게임 첫 시작 시 기본 점프 → 슬라이딩 → 벽 타기 → 천장 순서로
// 강제 학습시키는 가이드 시스템입니다.
// PlayerPrefs를 이용해 한 번만 실행합니다.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 튜토리얼 청크 프리팹 (순서대로) ===")]
        [Tooltip("1. 기본 점프: 낮은 허들")]
        [SerializeField] private GameObject jumpTutorialChunk;

        [Tooltip("2. 슬라이딩: 좁은 틈")]
        [SerializeField] private GameObject slideTutorialChunk;

        [Tooltip("3. 벽 타기: 바닥 끊기 + 우측 벽")]
        [SerializeField] private GameObject wallRunTutorialChunk;

        [Tooltip("4. 천장 타기: ㄷ자 벽")]
        [SerializeField] private GameObject ceilingTutorialChunk;

        [Header("=== 가이드 UI ===")]
        [Tooltip("가이드 텍스트 표시용 UI")]
        [SerializeField] private UnityEngine.UI.Text guideText;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private const string TutorialCompletedKey = "TutorialCompleted";
        private int currentStep;
        private bool isTutorialActive;

        // 튜토리얼 메시지
        private readonly string[] guideMessages = new[]
        {
            "[SPACE]  JUMP",
            "[CTRL]  SLIDE",
            "[SHIFT/RMB]  WALL RUN — 우측 벽으로 점프하여 질주!",
            "[SHIFT/RMB]  CEILING — 천장에 머리를 부딪혀라!"
        };

        // ──────────────────────────────────────────────
        // 초기화
        // ──────────────────────────────────────────────

        /// <summary>
        /// GameInstaller에서 호출. 튜토리얼 완료 여부를 확인하고 시작합니다.
        /// </summary>
        public void Initialize()
        {
            if (PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)
            {
                // 이미 클리어 → 튜토리얼 스킵
                isTutorialActive = false;
                gameObject.SetActive(false);
                return;
            }

            isTutorialActive = true;
            currentStep = 0;
            ShowCurrentStep();
        }

        // ──────────────────────────────────────────────
        // 단계 진행
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 튜토리얼 단계의 청크와 가이드 텍스트를 표시합니다.
        /// </summary>
        private void ShowCurrentStep()
        {
            if (!isTutorialActive || currentStep >= guideMessages.Length) return;

            if (guideText != null)
            {
                guideText.text = guideMessages[currentStep];
                guideText.gameObject.SetActive(true);
            }

            // TODO: 해당 단계의 튜토리얼 청크를 배치
            // SpawnTutorialChunk(currentStep);
        }

        /// <summary>
        /// 현재 단계를 클리어하고 다음 단계로 진행합니다.
        /// 각 튜토리얼 청크의 클리어 트리거가 이 메서드를 호출합니다.
        /// </summary>
        public void CompleteCurrentStep()
        {
            if (!isTutorialActive) return;

            currentStep++;

            if (currentStep >= guideMessages.Length)
            {
                CompleteTutorial();
                return;
            }

            ShowCurrentStep();
        }

        /// <summary>
        /// 튜토리얼 전체를 완료 처리합니다.
        /// </summary>
        private void CompleteTutorial()
        {
            isTutorialActive = false;
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();

            if (guideText != null)
                guideText.gameObject.SetActive(false);

            Debug.Log("[Tutorial] 튜토리얼 완료!");
        }

        /// <summary>튜토리얼이 현재 진행 중인가?</summary>
        public bool IsActive => isTutorialActive;
    }
}
