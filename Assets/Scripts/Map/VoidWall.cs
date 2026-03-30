// ============================================================================
// VoidWall.cs — 추격 레이저 벽 (Run or Die)
// 플레이어 등 뒤에서 최고 속도와 비슷한 속도로 쫓아옵니다.
// 멈추거나 걷기만 하면 잡혀서 게임 오버됩니다.
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.Map
{
    public class VoidWall : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 추격 설정 ===")]
        [Tooltip("Void 벽의 기본 추격 속도 (m/s). 플레이어 최고 속도와 비슷해야 함")]
        [SerializeField] private float chaseSpeed = 18f;

        [Tooltip("플레이어와의 초기 거리 (Z축)")]
        [SerializeField] private float initialOffset = 30f;

        [Tooltip("시간 경과에 따른 속도 증가율 (m/s²)")]
        [SerializeField] private float speedRampRate = 0.1f;

        [Header("=== 게임 오버 ===")]
        [Tooltip("Void 벽이 플레이어에게 이 거리 이내로 접근하면 게임 오버")]
        [SerializeField] private float killDistance = 1f;

        // ──────────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────────

        /// <summary>Void 벽에 잡혔을 때 (게임 오버). UIManager가 구독.</summary>
        public event Action OnVoidCaught;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private Transform playerTransform;
        private float currentSpeed;
        private bool isActive;

        // ──────────────────────────────────────────────
        // 초기화
        // ──────────────────────────────────────────────

        /// <summary>GameInstaller에서 호출. 플레이어 참조를 주입하고 시작합니다.</summary>
        public void Initialize(Transform player)
        {
            playerTransform = player;
            currentSpeed = chaseSpeed;

            // 플레이어 뒤에 초기 배치
            transform.position = new Vector3(
                player.position.x,
                player.position.y,
                player.position.z - initialOffset
            );

            isActive = true;
        }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Update()
        {
            if (!isActive || playerTransform == null) return;

            // 속도 서서히 증가 (시간 경과)
            currentSpeed += speedRampRate * Time.deltaTime;

            // 전진 (Z축 양의 방향)
            transform.position += Vector3.forward * (currentSpeed * Time.deltaTime);

            // 게임 오버 체크: 플레이어를 따라잡았는가?
            float distanceToPlayer = playerTransform.position.z - transform.position.z;
            if (distanceToPlayer <= killDistance)
            {
                isActive = false;
                OnVoidCaught?.Invoke();
            }
        }
    }
}
