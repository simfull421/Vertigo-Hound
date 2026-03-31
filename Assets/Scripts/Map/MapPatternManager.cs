// ============================================================================
// MapPatternManager.cs — 난이도 및 패턴 제어
// 게임 진행도에 따라 맵 청크 패턴을 결정하고 ChunkPooler에 생성을 요청합니다.
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.Map
{
    public class MapPatternManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("=== 패턴 설정 ===")]
        [Tooltip("사용 가능한 청크 프리팹 배열 (난이도순 정렬)")]
        [SerializeField] private GameObject[] chunkPrefabs;

        [Tooltip("청크 간 간격 (Z축 거리)")]
        [SerializeField] private float chunkSpacing = 50f;

        [Header("=== 난이도 곡선 ===")]
        [Tooltip("난이도 증가 커브 (X: 경과 시간 비율, Y: 난이도 0~1)")]
        [SerializeField] private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("최대 난이도에 도달하는 시간 (초)")]
        [SerializeField] private float maxDifficultyTime = 300f;

        // ──────────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────────

        /// <summary>청크 생성 요청. ChunkPooler가 구독하여 실제 생성을 수행합니다.</summary>
        public event Action<GameObject, Vector3> OnChunkRequested;

        // ──────────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────────

        [Header("=== 참조 ===")]
        [Tooltip("ChunkPooler 참조")]
        [SerializeField] private ChunkPooler chunkPooler;

        private float gameTimer;
        private int chunksSpawned;

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Update()
        {
            gameTimer += Time.deltaTime;

            // TODO: 플레이어 위치 기반으로 전방 청크 유지
            // CheckAndSpawnChunks();
        }

        // ──────────────────────────────────────────────
        // 청크 생성 로직
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 난이도에 맞는 청크 프리팹을 선택합니다.
        /// </summary>
        private GameObject SelectChunkPrefab()
        {
            if (chunkPrefabs == null || chunkPrefabs.Length == 0) return null;

            float difficultyRatio = Mathf.Clamp01(gameTimer / maxDifficultyTime);
            float difficulty = difficultyCurve.Evaluate(difficultyRatio);

            int index = Mathf.FloorToInt(difficulty * (chunkPrefabs.Length - 1));
            index = Mathf.Clamp(index, 0, chunkPrefabs.Length - 1);

            return chunkPrefabs[index];
        }

        /// <summary>
        /// 새 청크 생성을 요청합니다.
        /// </summary>
        public void RequestNextChunk()
        {
            GameObject prefab = SelectChunkPrefab();
            if (prefab == null) return;

            Vector3 spawnPosition = Vector3.forward * (chunksSpawned * chunkSpacing);
            chunksSpawned++;

            OnChunkRequested?.Invoke(prefab, spawnPosition);
        }
    }
}
