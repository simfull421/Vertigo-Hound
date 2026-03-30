// ============================================================================
// EventTriggers.cs — 맵 트리거 이벤트 발신 컴포넌트
// 수직 낙하 존, 중력 반전 존 등의 트리거 콜라이더에 부착합니다.
// 플레이어가 진입하면 CharacterFacade에 이벤트를 발신합니다.
// ★ 직접 호출 금지 — event 기반 통신 (coding.md 원칙)
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.Map
{
    [RequireComponent(typeof(BoxCollider))]
    public class EventTriggers : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 트리거 설정 ===")]
        [Tooltip("이 트리거의 종류 (FreeFall, 등)")]
        [SerializeField] private TriggerType triggerType = TriggerType.FreeFall;

        // ──────────────────────────────────────────────
        // 이벤트 (트리거 종류별)
        // ──────────────────────────────────────────────

        /// <summary>수직 낙하 트리거 진입</summary>
        public static event Action<Transform> OnFreeFallTrigger;

        // ──────────────────────────────────────────────
        // 트리거 감지
        // ──────────────────────────────────────────────

        private void Awake()
        {
            // 이 콜라이더를 트리거로 강제 설정
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // "Player" 태그로 필터링
            if (!other.CompareTag("Player")) return;

            switch (triggerType)
            {
                case TriggerType.FreeFall:
                    OnFreeFallTrigger?.Invoke(other.transform);
                    // CharacterFacade에 직접 알림 (대안: static event 구독)
                    var facade = other.GetComponent<VertigoHound.Player.CharacterFacade>();
                    if (facade != null)
                    {
                        facade.HandleTrigger("FreeFall");
                    }
                    break;
            }
        }
    }

    /// <summary>트리거 종류 열거형</summary>
    public enum TriggerType
    {
        /// <summary>수직 자유낙하 존</summary>
        FreeFall,

        /// <summary>튜토리얼 가이드 트리거</summary>
        Tutorial
    }
}
