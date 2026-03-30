// ============================================================================
// PlayerController.cs — New Input System 입력 처리 전담
// 키보드/마우스 입력을 읽어 CharacterFacade에 전달합니다.
// ★ 직접 물리를 건드리지 않고, Facade의 공개 메서드만 호출합니다.
// ★ Grab 버튼(우클릭/Shift): IsTouchingWall/Ceiling + Grab 유지 → 벽/천장 전환
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace VertigoHound.Player
{
    [RequireComponent(typeof(CharacterFacade))]
    public class PlayerController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 참조
        // ──────────────────────────────────────────────

        [Header("=== 카메라 참조 ===")]
        [Tooltip("1인칭 카메라 (CameraController가 붙은 객체)")]
        [SerializeField] private CameraController cameraController;

        // ──────────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────────

        private CharacterFacade facade;

        // ──────────────────────────────────────────────
        // 입력 상태 (외부에서 읽기 전용)
        // ──────────────────────────────────────────────

        /// <summary>이동 입력 벡터 (WASD)</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Grab 버튼이 눌려 있는가? (우클릭 또는 Shift)</summary>
        public bool IsGrabbing { get; private set; }

        /// <summary>이번 프레임에 점프 키를 눌렀는가?</summary>
        public bool JumpPressed { get; private set; }

        /// <summary>이번 프레임에 슬라이드 키를 눌렀는가?</summary>
        public bool SlidePressed { get; private set; }

        /// <summary>슬라이드 키를 유지하고 있는가?</summary>
        public bool SlideHeld { get; private set; }

        /// <summary>마우스 시선 이동량</summary>
        public Vector2 LookInput { get; private set; }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            facade = GetComponent<CharacterFacade>();
        }

        private void Update()
        {
            ReadInputs();
            HandleLook();
            HandleMovement();
            HandleActions();
        }

        // ──────────────────────────────────────────────
        // 입력 읽기 (New Input System — Polling 방식)
        // ──────────────────────────────────────────────

        private void ReadInputs()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // WASD 이동
            Vector2 rawMove = Vector2.zero;
            if (kb.wKey.isPressed) rawMove.y += 1f;
            if (kb.sKey.isPressed) rawMove.y -= 1f;
            if (kb.aKey.isPressed) rawMove.x -= 1f;
            if (kb.dKey.isPressed) rawMove.x += 1f;
            MoveInput = rawMove.normalized;

            // 마우스 시선
            LookInput = mouse.delta.ReadValue();

            // 점프 (이번 프레임에 눌렸는가?)
            JumpPressed = kb.spaceKey.wasPressedThisFrame;

            // 슬라이드 (C 또는 Ctrl)
            SlidePressed = kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
            SlideHeld = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;

            // Grab 버튼 (우클릭 또는 Shift 유지)
            IsGrabbing = mouse.rightButton.isPressed || kb.leftShiftKey.isPressed;
        }

        // ──────────────────────────────────────────────
        // 시선 처리
        // ──────────────────────────────────────────────

        private void HandleLook()
        {
            if (cameraController == null) return;
            cameraController.Look(LookInput.x, LookInput.y, transform);
        }

        // ──────────────────────────────────────────────
        // 이동 처리 → Facade에 위임
        // ──────────────────────────────────────────────

        private void HandleMovement()
        {
            // 카메라 기준 이동 방향 계산
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 moveDirection = (forward * MoveInput.y + right * MoveInput.x).normalized;

            facade.RequestMove(moveDirection);
        }

        // ──────────────────────────────────────────────
        // 액션 처리 → Facade에 위임
        // ──────────────────────────────────────────────

        private void HandleActions()
        {
            if (JumpPressed)
            {
                facade.RequestJump();
            }

            if (SlidePressed)
            {
                facade.RequestSlide();
            }

            // Grab 상태는 Facade가 매 프레임 읽어감 (프로퍼티 노출)
        }
    }
}
