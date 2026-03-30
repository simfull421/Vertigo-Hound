// ============================================================================
// ScoreManager.cs — 스타일 콤보 배수 및 점수 연산
// 공중/벽/천장 체류 시간에 비례해 콤보 배수가 증가하는 로직.
// 벽 차기 연계 시 배수 폭발 증가 (srs.md §6 반영).
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.UI
{
    public class ScoreManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 기본 점수 ===")]
        [Tooltip("벽(패턴) 하나를 무사히 통과할 때마다 얻는 기본 점수")]
        [SerializeField] private int baseScore = 100;

        [Header("=== 콤보 설정 ===")]
        [Tooltip("공중/벽/천장에 있을 때 초당 콤보 배수 증가량")]
        [SerializeField] private float comboGainPerSecond = 0.3f;

        [Tooltip("바닥에 있을 때의 콤보 배수 (고정)")]
        [SerializeField] private float groundComboMultiplier = 1.0f;

        [Tooltip("벽 차기 연계 시 추가 콤보 배수")]
        [SerializeField] private float wallJumpComboBonus = 0.5f;

        [Tooltip("콤보 배수 최대값")]
        [SerializeField] private float maxComboMultiplier = 10f;

        [Tooltip("바닥에 착지하면 콤보 배수가 리셋되는 딜레이(초)")]
        [SerializeField] private float comboResetDelay = 2f;

        // ──────────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────────

        /// <summary>점수가 업데이트될 때 방송. UIManager가 구독합니다.</summary>
        public event Action<int> OnScoreUpdate;

        /// <summary>콤보 배수가 변경될 때 방송.</summary>
        public event Action<float> OnComboUpdate;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private int totalScore;
        private float currentCombo = 1f;
        private bool isOnGround;
        private float groundTimer;

        // ──────────────────────────────────────────────
        // 초기화 (이벤트 구독)
        // ──────────────────────────────────────────────

        /// <summary>
        /// GameInstaller에서 호출. CharacterFacade의 이벤트에 구독합니다.
        /// </summary>
        public void Initialize(Player.CharacterFacade facade)
        {
            facade.OnStateChanged += HandleStateChanged;
            facade.OnActionPerformed += HandleAction;
        }

        // ──────────────────────────────────────────────
        // 이벤트 핸들러
        // ──────────────────────────────────────────────

        private void HandleStateChanged(Player.StateId stateId)
        {
            // 바닥에 있는 상태인가?
            isOnGround = (stateId == Player.StateId.Run || stateId == Player.StateId.Slide);

            if (isOnGround)
            {
                groundTimer = 0f;
            }
        }

        private void HandleAction(string actionName)
        {
            switch (actionName)
            {
                case "WallJump":
                    // 벽 차기 연계 보너스
                    currentCombo += wallJumpComboBonus;
                    currentCombo = Mathf.Min(currentCombo, maxComboMultiplier);
                    OnComboUpdate?.Invoke(currentCombo);
                    break;

                case "FreeFallLanded":
                    // 번지 착지 보너스 점수
                    AddScore(baseScore);
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Update()
        {
            if (isOnGround)
            {
                // 바닥에 있으면 콤보 서서히 리셋
                groundTimer += Time.deltaTime;
                if (groundTimer >= comboResetDelay)
                {
                    currentCombo = Mathf.MoveTowards(currentCombo, groundComboMultiplier, Time.deltaTime * 2f);
                    OnComboUpdate?.Invoke(currentCombo);
                }
            }
            else
            {
                // 공중/벽/천장에 있으면 콤보 증가
                currentCombo += comboGainPerSecond * Time.deltaTime;
                currentCombo = Mathf.Min(currentCombo, maxComboMultiplier);
                OnComboUpdate?.Invoke(currentCombo);
            }
        }

        // ──────────────────────────────────────────────
        // 점수 추가
        // ──────────────────────────────────────────────

        /// <summary>
        /// 기본 점수에 현재 콤보 배수를 곱해 총 점수에 추가합니다.
        /// </summary>
        public void AddScore(int points)
        {
            int scoredPoints = Mathf.RoundToInt(points * currentCombo);
            totalScore += scoredPoints;
            OnScoreUpdate?.Invoke(totalScore);
        }

        /// <summary>벽 통과 시 기본 점수 추가</summary>
        public void OnPatternCleared()
        {
            AddScore(baseScore);
        }
    }
}
