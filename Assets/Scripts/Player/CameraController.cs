// ============================================================================
// CameraController.cs — 1인칭 카메라 연출 전담 컴포넌트
// FOV 변경, Z축 Tilt, 백덤블링, 180도 스냅턴, 셰이크, 높이 변경 등
// 모든 시각 연출을 중앙에서 관리합니다.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 조절 변수
        // ──────────────────────────────────────────────

        [Header("=== 기본 설정 ===")]
        [Tooltip("기본 카메라 높이 (이족보행 Eye Height)")]
        [SerializeField] private float defaultEyeHeight = 1.7f;

        [Tooltip("사족보행 시 카메라 높이")]
        [SerializeField] private float quadEyeHeight = 0.8f;

        [Tooltip("마우스 감도")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Header("=== FOV ===")]
        [Tooltip("기본 시야각")]
        [SerializeField] private float defaultFOV = 75f;

        [Tooltip("최대 질주 시 시야각")]
        [SerializeField] private float sprintFOV = 90f;

        [Tooltip("슬라이딩 진입 시 순간 팽창 시야각")]
        [SerializeField] private float slideFOV = 100f;

        [Tooltip("FOV 전환 속도 (Lerp 계수)")]
        [SerializeField] private float fovLerpSpeed = 8f;

        [Header("=== Tilt (Z축 기울기) ===")]
        [Tooltip("벽 타기 시 카메라 Z축 기울기 (도)")]
        [SerializeField] private float wallRunTiltAngle = 18f;

        [Tooltip("Tilt 전환 속도")]
        [SerializeField] private float tiltLerpSpeed = 10f;

        [Header("=== 셰이크 (화면 흔들림) ===")]
        [Tooltip("최대 셰이크 강도 (질주 중)")]
        [SerializeField] private float maxShakeIntensity = 0.05f;

        [Header("=== 백덤블링 (FreeFall 연출) ===")]
        [Tooltip("백덤블링 180도 회전에 걸리는 시간 (초)")]
        [SerializeField] private float backflipDuration = 0.4f;

        [Header("=== 스냅턴 (천장 중력 반전 연출) ===")]
        [Tooltip("180도 스냅턴에 걸리는 시간 (초)")]
        [SerializeField] private float snapTurnDuration = 0.08f;

        [Header("=== 높이 전환 ===")]
        [Tooltip("카메라 높이 전환 속도")]
        [SerializeField] private float heightLerpSpeed = 5f;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private Camera cam;
        private float targetFOV;
        private float targetTiltZ;
        private float targetHeight;
        private float currentTiltZ;
        private float currentHeight;
        private float xRotation; // 상하 시선 각도

        // 백덤블링 / 스냅턴 코루틴 제어
        private Coroutine activeRotationRoutine;

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            cam = GetComponent<Camera>();
            targetFOV = defaultFOV;
            targetHeight = defaultEyeHeight;
            currentHeight = defaultEyeHeight;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void LateUpdate()
        {
            // FOV 부드러운 전환
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);

            // Tilt 부드러운 전환
            currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltZ, tiltLerpSpeed * Time.deltaTime);

            // 높이 부드러운 전환
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, heightLerpSpeed * Time.deltaTime);

            // 로컬 포지션 Y에 높이 적용
            transform.localPosition = new Vector3(0f, currentHeight, 0f);
        }

        // ──────────────────────────────────────────────
        // 마우스 시선 제어 (PlayerController에서 호출)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 마우스 입력을 받아 카메라 좌우/상하 시선을 제어합니다.
        /// </summary>
        /// <param name="mouseX">마우스 좌우 이동량</param>
        /// <param name="mouseY">마우스 상하 이동량</param>
        /// <param name="playerTransform">플레이어 본체 Transform (좌우 회전용)</param>
        public void Look(float mouseX, float mouseY, Transform playerTransform)
        {
            // 상하 시선 (카메라 자체 X축 회전)
            xRotation -= mouseY * mouseSensitivity;
            xRotation = Mathf.Clamp(xRotation, -89f, 89f);

            // 좌우 시선 (플레이어 본체 Y축 회전)
            playerTransform.Rotate(Vector3.up, mouseX * mouseSensitivity);

            // 카메라 최종 로컬 회전 적용 (Tilt 포함)
            transform.localRotation = Quaternion.Euler(xRotation, 0f, currentTiltZ);
        }

        // ──────────────────────────────────────────────
        // FOV 제어
        // ──────────────────────────────────────────────

        /// <summary>목표 FOV를 설정합니다 (부드럽게 전환)</summary>
        public void SetFOV(float fov) => targetFOV = fov;

        /// <summary>기본 FOV로 복구합니다</summary>
        public void ResetFOV() => targetFOV = defaultFOV;

        /// <summary>속도에 비례한 FOV를 적용합니다 (질주 중)</summary>
        public void ApplySpeedFOV(float speedRatio)
        {
            targetFOV = Mathf.Lerp(defaultFOV, sprintFOV, speedRatio);
        }

        /// <summary>슬라이딩 FOV 팽창</summary>
        public void ApplySlideFOV() => targetFOV = slideFOV;

        // ──────────────────────────────────────────────
        // Tilt 제어 (Z축 기울기)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 벽 타기 방향에 따라 카메라를 기울입니다.
        /// </summary>
        /// <param name="direction">1 = 우측 벽, -1 = 좌측 벽</param>
        public void ApplyWallRunTilt(int direction)
        {
            targetTiltZ = -direction * wallRunTiltAngle;
        }

        /// <summary>Tilt를 0도로 복구합니다</summary>
        public void ResetTilt() => targetTiltZ = 0f;

        // ──────────────────────────────────────────────
        // 카메라 높이 제어
        // ──────────────────────────────────────────────

        /// <summary>카메라 높이를 사족보행 높이로 낮춥니다</summary>
        public void SetQuadHeight() => targetHeight = quadEyeHeight;

        /// <summary>카메라 높이를 기본 이족보행으로 복구합니다</summary>
        public void ResetHeight() => targetHeight = defaultEyeHeight;

        /// <summary>카메라 높이를 직접 설정합니다</summary>
        public void SetHeight(float height) => targetHeight = height;

        // ──────────────────────────────────────────────
        // 셰이크 (화면 흔들림)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 속도에 비례한 화면 셰이크를 적용합니다.
        /// RunState의 Tick에서 매 프레임 호출합니다.
        /// </summary>
        /// <param name="intensity">0~1 사이의 강도 비율</param>
        public void ApplyShake(float intensity)
        {
            float shake = intensity * maxShakeIntensity;
            Vector3 offset = new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                0f
            );
            // 기존 로컬 포지션에 셰이크 오프셋만 더함
            transform.localPosition = new Vector3(offset.x, currentHeight + offset.y, 0f);
        }

        // ──────────────────────────────────────────────
        // 특수 연출 (코루틴 기반)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 백덤블링 연출: 카메라 X축을 180도 회전시킵니다.
        /// FreeFallState 진입 시 호출합니다.
        /// </summary>
        public void PlayBackflipSequence()
        {
            if (activeRotationRoutine != null) StopCoroutine(activeRotationRoutine);
            activeRotationRoutine = StartCoroutine(RotateXOverTime(180f, backflipDuration));
        }

        /// <summary>
        /// 스냅턴 연출: 카메라 Z축을 180도 회전시킵니다.
        /// CeilingState 진입 시 중력 반전과 함께 호출합니다.
        /// </summary>
        public void PlaySnapInvert()
        {
            if (activeRotationRoutine != null) StopCoroutine(activeRotationRoutine);
            activeRotationRoutine = StartCoroutine(RotateZOverTime(180f, snapTurnDuration));
        }

        /// <summary>
        /// 스냅턴 복귀: 천장에서 나올 때 카메라를 원래대로 되돌립니다.
        /// </summary>
        public void PlaySnapRestore()
        {
            if (activeRotationRoutine != null) StopCoroutine(activeRotationRoutine);
            activeRotationRoutine = StartCoroutine(RotateZOverTime(-180f, snapTurnDuration));
        }

        // ──────────────────────────────────────────────
        // 코루틴 헬퍼
        // ──────────────────────────────────────────────

        private System.Collections.IEnumerator RotateXOverTime(float angle, float duration)
        {
            float elapsed = 0f;
            float startX = xRotation;
            float endX = startX + angle;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                xRotation = Mathf.Lerp(startX, endX, t);
                yield return null;
            }
            xRotation = endX;
            activeRotationRoutine = null;
        }

        private System.Collections.IEnumerator RotateZOverTime(float angle, float duration)
        {
            float elapsed = 0f;
            float startZ = currentTiltZ;
            float endZ = startZ + angle;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                currentTiltZ = Mathf.Lerp(startZ, endZ, t);
                yield return null;
            }
            currentTiltZ = endZ;
            activeRotationRoutine = null;
        }
    }
}
