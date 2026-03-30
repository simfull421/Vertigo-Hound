// ============================================================================
// UIManager.cs — HUD · 점수 · 콤보 표시 관리
// 모든 UI 업데이트를 중앙에서 처리합니다.
// ★ 직접 게임 로직을 수행하지 않음. event 구독만으로 데이터를 받음.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace VertigoHound.UI
{
    public class UIManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 참조 (UI 요소)
        // ──────────────────────────────────────────────

        [Header("=== HUD 요소 ===")]
        [Tooltip("현재 점수 텍스트")]
        [SerializeField] private Text scoreText;

        [Tooltip("콤보 배수 텍스트 (우측 상단)")]
        [SerializeField] private Text comboText;

        [Tooltip("현재 상태 표시 텍스트 (디버그용)")]
        [SerializeField] private Text stateText;

        [Tooltip("속도 표시 텍스트")]
        [SerializeField] private Text speedText;

        [Header("=== 게임 오버 패널 ===")]
        [Tooltip("게임 오버 UI 패널")]
        [SerializeField] private GameObject gameOverPanel;

        // ──────────────────────────────────────────────
        // 공개 메서드 (이벤트 핸들러로 등록)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 점수를 업데이트합니다. ScoreManager.OnScoreUpdate 이벤트에 구독합니다.
        /// </summary>
        public void UpdateScore(int score)
        {
            if (scoreText != null)
                scoreText.text = $"SCORE: {score:N0}";
        }

        /// <summary>
        /// 콤보 배수를 업데이트합니다. CharacterFacade.OnComboUpdate 이벤트에 구독합니다.
        /// </summary>
        public void UpdateCombo(float multiplier)
        {
            if (comboText != null)
                comboText.text = $"x{multiplier:F1}";
        }

        /// <summary>
        /// 현재 상태를 표시합니다 (디버그). CharacterFacade.OnStateChanged에 구독합니다.
        /// </summary>
        public void UpdateState(Player.StateId stateId)
        {
            if (stateText != null)
                stateText.text = stateId.ToString();
        }

        /// <summary>
        /// 속도를 업데이트합니다. 매 프레임 호출 또는 이벤트 기반.
        /// </summary>
        public void UpdateSpeed(float speed)
        {
            if (speedText != null)
                speedText.text = $"{speed:F0} m/s";
        }

        /// <summary>
        /// 게임 오버 UI를 표시합니다. VoidWall.OnVoidCaught에 구독합니다.
        /// </summary>
        public void ShowGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Time.timeScale = 0f;
            }
        }
    }
}
