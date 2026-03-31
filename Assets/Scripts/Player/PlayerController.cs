// ============================================================================
// PlayerController.cs — Input Action Asset 기반 Event-driven 입력 처리
// ★ Keyboard.current 직접 폴링 폐지 → InputActionAsset 콜백 방식 전면 전환
// ★ Grab 버튼: Sprint 액션(Shift) + 우클릭(Attack 대체 또는 별도 바인딩)
// ★ 입력 이벤트를 직접 물리에 적용하지 않고, 상태값만 갱신하여
//   CharacterFacade가 State에 전달합니다.
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

        [Header("=== Input Action Asset ===")]
        [Tooltip("InputSystem_Actions 에셋을 여기에 드래그하세요")]
        [SerializeField] private InputActionAsset inputActions;

        // ──────────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────────

        private CharacterFacade facade;

        // Input Actions (Player 맵에서 가져옴)
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction slideAction;   // "Crouch" 액션을 슬라이드로 사용
        private InputAction grabAction;    // "Sprint" 액션(Shift)을 Grab으로 사용

        // ──────────────────────────────────────────────
        // 입력 상태 (외부에서 읽기 전용)
        // CharacterFacade → State 클래스들이 이 값을 읽습니다.
        // ──────────────────────────────────────────────

        /// <summary>이동 입력 벡터 (WASD / 스틱)</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>마우스 / 스틱 시선 이동량</summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>Grab 버튼이 눌려 있는가? (Shift 또는 우클릭)</summary>
        public bool IsGrabbing { get; private set; }

        /// <summary>이번 프레임에 점프가 트리거되었는가?</summary>
        public bool JumpTriggered { get; private set; }

        /// <summary>이번 프레임에 슬라이드가 트리거되었는가?</summary>
        public bool SlideTriggered { get; private set; }

        /// <summary>슬라이드 키를 유지하고 있는가?</summary>
        public bool SlideHeld { get; private set; }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            facade = GetComponent<CharacterFacade>();
            SetupActions();
        }

        private void OnEnable()
        {
            EnableActions();
            SubscribeCallbacks();
        }

        private void OnDisable()
        {
            UnsubscribeCallbacks();
            DisableActions();
        }

        private void Update()
        {
            // 연속 입력(Value 타입)은 매 프레임 읽기
            MoveInput = moveAction.ReadValue<Vector2>();
            LookInput = lookAction.ReadValue<Vector2>();
            SlideHeld = slideAction.IsPressed();

            // ★ 유지(Hold) 판정인 Grab은 콜백 대신 매 프레임 눌림 여부를 확실하게 판별
            IsGrabbing = grabAction.IsPressed();

            // 이동 방향을 Facade에 전달
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 moveDir = (forward * MoveInput.y + right * MoveInput.x).normalized;
            facade.RequestMove(moveDir);
        }

        private void LateUpdate()
        {
            // 원샷 플래그는 프레임 끝에서 리셋
            // (State의 Tick이 Update에서 실행되므로 LateUpdate에서 리셋)
            JumpTriggered = false;
            SlideTriggered = false;
        }

        // ──────────────────────────────────────────────
        // Input Action 설정
        // ──────────────────────────────────────────────

        private void SetupActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerController] InputActionAsset이 할당되지 않았습니다!");
                return;
            }

            var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);

            moveAction  = playerMap.FindAction("Move",   throwIfNotFound: true);
            lookAction  = playerMap.FindAction("Look",   throwIfNotFound: true);
            jumpAction  = playerMap.FindAction("Jump",   throwIfNotFound: true);
            slideAction = playerMap.FindAction("Crouch", throwIfNotFound: true);
            grabAction  = playerMap.FindAction("Sprint", throwIfNotFound: true);

            Debug.Log($"[PlayerController] Input Actions 매핑 완료 — " +
                      $"Jump={jumpAction?.name}, Slide={slideAction?.name}(Crouch), " +
                      $"Grab={grabAction?.name}(Sprint=LeftShift)");
        }

        private void EnableActions()
        {
            moveAction?.Enable();
            lookAction?.Enable();
            jumpAction?.Enable();
            slideAction?.Enable();
            grabAction?.Enable();
        }

        private void DisableActions()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            jumpAction?.Disable();
            slideAction?.Disable();
            grabAction?.Disable();
        }

        // ──────────────────────────────────────────────
        // 콜백 구독 / 해제
        // ──────────────────────────────────────────────

        private void SubscribeCallbacks()
        {
            if (jumpAction != null)
            {
                jumpAction.performed += OnJump;
            }

            if (slideAction != null)
            {
                slideAction.performed += OnSlide;
            }
        }

        private void UnsubscribeCallbacks()
        {
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJump;
            }

            if (slideAction != null)
            {
                slideAction.performed -= OnSlide;
            }
        }

        // ──────────────────────────────────────────────
        // Input Action 콜백 핸들러
        // ──────────────────────────────────────────────

        /// <summary>Jump 액션이 performed 됐을 때 (Space, 게임패드 등)</summary>
        private void OnJump(InputAction.CallbackContext context)
        {
            // ★ Input Phase 필터링: 반드시 performed(눌린 순간) 단계에서만 실행
            if (!context.performed) return;

            JumpTriggered = true;
            Debug.Log("[PlayerController] OnJump 콜백 → facade.RequestJump() 즉시 호출");
            // 푸시 방식: 콜백에서 즉시 Facade → StateMachine → State로 전달
            facade.RequestJump();
        }

        /// <summary>Slide(Crouch) 액션이 performed 됐을 때 (C, 게임패드 등)</summary>
        private void OnSlide(InputAction.CallbackContext context)
        {
            // ★ Input Phase 필터링
            if (!context.performed) return;

            SlideTriggered = true;
            // 푸시 방식
            facade.RequestSlide();
        }
    }
}
