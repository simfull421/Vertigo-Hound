// ============================================================================
// CameraController.cs — 1인칭 카메라 연출 전담 컴포넌트
// ★ 하이어라키: Player → CameraRoot(이 스크립트) → MainCamera
// CameraRoot의 회전(시선)과 위치(높이)를 SmoothDamp로 부드럽게 보간합니다.
// MainCamera(자식)에서 FOV, 셰이크 오프셋을 적용합니다.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player
{
    public class CameraController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 참조
        // ──────────────────────────────────────────────

        [Header("=== 카메라 참조 ===")]
        [Tooltip("CameraRoot의 자식인 MainCamera (Camera 컴포넌트가 붙은 객체)")]
        [SerializeField] private Camera mainCamera;

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

        [Header("=== SmoothDamp 보간 ===")]
        [Tooltip("높이 전환 SmoothDamp 시간 (초). 낮을수록 빠름")]
        [SerializeField] private float heightSmoothTime = 0.15f;

        [Tooltip("회전 전환 SmoothDamp 시간 (초)")]
        [SerializeField] private float rotationSmoothTime = 0.02f;

        // ──────────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────────

        private float targetFOV;
        private float targetTiltZ;
        private float targetHeight;
        private float currentTiltZ;
        private float currentHeight;
        private float xRotation; // 상하 시선 각도

        // SmoothDamp 속도 변수 (ref로 전달)
        private float heightVelocity;
        private float tiltVelocity;

        // 백덤블링 / 스냅턴 코루틴 제어
        private Coroutine activeRotationRoutine;

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            // MainCamera가 인스펙터에서 할당되지 않으면 자식에서 찾기
            if (mainCamera == null)
                mainCamera = GetComponentInChildren<Camera>();

            targetFOV = defaultFOV;
            targetHeight = defaultEyeHeight;
            currentHeight = defaultEyeHeight;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void LateUpdate()
        {
            // ── FOV 부드러운 전환 ──
            if (mainCamera != null)
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);

            // ── Tilt SmoothDamp ──
            currentTiltZ = Mathf.SmoothDamp(currentTiltZ, targetTiltZ, ref tiltVelocity, rotationSmoothTime);

            // ── 높이 SmoothDamp (떨림 방지) ──
            currentHeight = Mathf.SmoothDamp(currentHeight, targetHeight, ref heightVelocity, heightSmoothTime);

            // ── CameraRoot 로컬 포지션에 높이 적용 ──
            transform.localPosition = new Vector3(0f, currentHeight, 0f);

            // ── CameraRoot 로컬 회전 적용 (Pitch + Tilt) ──
            transform.localRotation = Quaternion.Euler(xRotation, 0f, currentTiltZ);
        }

        // ──────────────────────────────────────────────
        // 마우스 시선 제어 (PlayerController에서 호출)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 마우스/스틱 입력을 받아 시선을 제어합니다.
        /// CameraRoot의 X축(Pitch)을 회전하고, 플레이어 본체의 Y축(Yaw)을 회전합니다.
        /// </summary>
        /// <param name="mouseX">수평 이동량</param>
        /// <param name="mouseY">수직 이동량</param>
        /// <param name="playerTransform">플레이어 본체 Transform (좌우 회전용)</param>
        public void Look(float mouseX, float mouseY, Transform playerTransform)
        {
            // 상하 시선 (CameraRoot X축 회전)
            xRotation -= mouseY * mouseSensitivity;
            xRotation = Mathf.Clamp(xRotation, -89f, 89f);

            // 좌우 시선 (플레이어 본체 Y축 회전)
            playerTransform.Rotate(Vector3.up, mouseX * mouseSensitivity);
        }

        // ──────────────────────────────────────────────
        // FOV 제어
        // ──────────────────────────────────────────────

        public void SetFOV(float fov) => targetFOV = fov;
        public void ResetFOV() => targetFOV = defaultFOV;

        public void ApplySpeedFOV(float speedRatio)
        {
            targetFOV = Mathf.Lerp(defaultFOV, sprintFOV, speedRatio);
        }

        public void ApplySlideFOV() => targetFOV = slideFOV;

        // ──────────────────────────────────────────────
        // Tilt 제어 (Z축 기울기)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 벽 타기 틸트. Unity Z축: +(양수)=반시계=왼쪽기울임, -(음수)=시계=오른쪽기울임.
        /// 왼쪽 벽(direction=-1) → 오른쪽으로 기울임(Z-) = direction * angle = -angle ✓
        /// 오른쪽 벽(direction=+1) → 왼쪽으로 기울임(Z+) = direction * angle = +angle ✓
        /// </summary>
        public void ApplyWallRunTilt(int direction)
        {
            // direction: +1 = 오른쪽 벽 → Z+ (왼쪽 기울임)
            //            -1 = 왼쪽 벽  → Z- (오른쪽 기울임)
            targetTiltZ = direction * wallRunTiltAngle;
            Debug.Log($"[CameraController] Tilt — direction={direction}, " +
                      $"targetZ={targetTiltZ:F1}° ({(direction > 0 ? "왼쪽 기울임(오른쪽벽)" : "오른쪽 기울임(왼쪽벽)")})");
        }

        public void ResetTilt() => targetTiltZ = 0f;

        // ──────────────────────────────────────────────
        // 카메라 높이 제어
        // ──────────────────────────────────────────────

        public void SetQuadHeight() => targetHeight = quadEyeHeight;
        public void ResetHeight() => targetHeight = defaultEyeHeight;
        public void SetHeight(float height) => targetHeight = height;

        // ──────────────────────────────────────────────
        // 셰이크 (화면 흔들림) — MainCamera 로컬 오프셋
        // ──────────────────────────────────────────────

        /// <summary>
        /// 속도에 비례한 화면 셰이크. MainCamera의 로컬 위치를 오프셋합니다.
        /// CameraRoot 자체는 흔들지 않으므로 SmoothDamp와 충돌하지 않습니다.
        /// </summary>
        public void ApplyShake(float intensity)
        {
            if (mainCamera == null) return;
            float shake = intensity * maxShakeIntensity;
            Vector3 offset = new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                0f
            );
            mainCamera.transform.localPosition = offset;
        }

        /// <summary>셰이크 오프셋을 리셋합니다.</summary>
        public void ResetShake()
        {
            if (mainCamera != null)
                mainCamera.transform.localPosition = Vector3.zero;
        }

        // ──────────────────────────────────────────────
        // 특수 연출 (코루틴 기반)
        // ──────────────────────────────────────────────

        public void PlayBackflipSequence()
        {
            if (activeRotationRoutine != null) StopCoroutine(activeRotationRoutine);
            activeRotationRoutine = StartCoroutine(RotateXOverTime(180f, backflipDuration));
        }

        public void PlaySnapInvert()
        {
            if (activeRotationRoutine != null) StopCoroutine(activeRotationRoutine);
            activeRotationRoutine = StartCoroutine(RotateZOverTime(180f, snapTurnDuration));
        }

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
