// ============================================================================
// ChunkPooler.cs — Object Pooling 최적화
// 청크를 미리 생성(Pool)하고, 필요할 때 꺼내 쓰고(Dequeue),
// 플레이어 통과 후 회수(Enqueue)하여 메모리 부하를 방지합니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace VertigoHound.Map
{
    public class ChunkPooler : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 풀 설정 ===")]
        [Tooltip("풀에 미리 생성할 청크 수")]
        [SerializeField] private int initialPoolSize = 10;

        [Tooltip("청크 프리팹 (기본 빈 청크)")]
        [SerializeField] private GameObject defaultChunkPrefab;

        [Header("=== 자동 회수 ===")]
        [Tooltip("플레이어 뒤로 이 거리만큼 벗어나면 자동 회수")]
        [SerializeField] private float despawnBehindDistance = 80f;

        // ──────────────────────────────────────────────
        // 내부 자료구조
        // ──────────────────────────────────────────────

        private readonly Queue<GameObject> pool = new Queue<GameObject>();
        private readonly List<GameObject> activeChunks = new List<GameObject>();

        private Transform playerTransform;

        // ──────────────────────────────────────────────
        // 초기화
        // ──────────────────────────────────────────────

        /// <summary>
        /// GameInstaller에서 호출. 플레이어 참조를 주입하고 풀을 초기화합니다.
        /// </summary>
        public void Initialize(Transform player)
        {
            playerTransform = player;
            PrewarmPool();
        }

        private void PrewarmPool()
        {
            if (defaultChunkPrefab == null) return;

            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject chunk = Instantiate(defaultChunkPrefab, transform);
                chunk.SetActive(false);
                pool.Enqueue(chunk);
            }
        }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Update()
        {
            if (playerTransform == null) return;
            DespawnBehindPlayer();
        }

        // ──────────────────────────────────────────────
        // Spawn (꺼내기)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 풀에서 청크를 꺼내 지정 위치에 배치합니다.
        /// 풀이 비어있으면 새로 생성합니다.
        /// </summary>
        public GameObject SpawnChunk(Vector3 position)
        {
            GameObject chunk;

            if (pool.Count > 0)
            {
                chunk = pool.Dequeue();
            }
            else
            {
                // 풀 부족 시 새로 생성 (경고)
                Debug.LogWarning("[ChunkPooler] 풀이 비어있어 새 청크를 생성합니다.");
                chunk = Instantiate(defaultChunkPrefab, transform);
            }

            chunk.transform.position = position;
            chunk.SetActive(true);
            activeChunks.Add(chunk);

            return chunk;
        }

        // ──────────────────────────────────────────────
        // Despawn (회수)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 플레이어 뒤로 벗어난 청크를 자동 회수합니다.
        /// </summary>
        private void DespawnBehindPlayer()
        {
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                float distanceBehind = playerTransform.position.z - activeChunks[i].transform.position.z;
                if (distanceBehind > despawnBehindDistance)
                {
                    ReturnToPool(activeChunks[i]);
                    activeChunks.RemoveAt(i);
                }
            }
        }

        /// <summary>청크를 비활성화하고 풀에 반환합니다.</summary>
        private void ReturnToPool(GameObject chunk)
        {
            chunk.SetActive(false);
            pool.Enqueue(chunk);
        }
    }
}
