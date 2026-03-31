// ============================================================================
// CharacterFacade.cs — Facade 패턴 (외부 API 단일 창구)
// 외부 시스템(Map, UI, Tutorial)은 이 클래스를 통해서만 플레이어와 소통합니다.
// ★ State 클래스들은 입력 이벤트를 직접 듣지 않고, Facade를 통해 전달받은
//   상태값(JumpTriggered, IsGrabbing 등)을 읽어서 동작합니다.
// ============================================================================

using System;
using UnityEngine;

namespace VertigoHound.Player
{
    [RequireComponent(typeof(PhysicsMotor))]
    [RequireComponent(typeof(PlayerController))]
    public class CharacterFacade : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 이벤트 (느슨한 결합 — coding.md 원칙)
        // ──────────────────────────────────────────────

        /// <summary>상태가 변경될 때 방송. UI가 구독하여 HUD를 업데이트합니다.</summary>
        public event Action<StateId> OnStateChanged;

        /// <summary>스타일 콤보 배수가 변경될 때 방송.</summary>
        public event Action<float> OnComboUpdate;

        /// <summary>플레이어가 특정 액션을 수행했을 때 방송 (점수 계산용).</summary>
        public event Action<string> OnActionPerformed;

        // ──────────────────────────────────────────────
        // 인스펙터 참조
        // ──────────────────────────────────────────────

        [Header("=== 하위 컴포넌트 참조 ===")]
        [Tooltip("CameraRoot에 붙은 CameraController")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("1인칭 애니메이션 컨트롤러 (없으면 null 허용)")]
        [SerializeField] private AnimationController animationController;

        // ──────────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────────

        private CharacterStateMachine stateMachine;
        private PhysicsMotor motor;
        private PlayerController playerInput;

        // ──────────────────────────────────────────────
        // 외부에서 읽기 전용 접근
        // ──────────────────────────────────────────────

        /// <summary>현재 FSM 상태 ID</summary>
        public StateId CurrentStateId => stateMachine.CurrentStateId;

        /// <summary>PhysicsMotor 접근 (State 클래스 전용)</summary>
        public PhysicsMotor Motor => motor;

        /// <summary>CameraController 접근 (State 클래스 전용, null 가능)</summary>
        public CameraController Camera => cameraController;

        /// <summary>AnimationController 접근 (State 클래스 전용, null 가능)</summary>
        public AnimationController Animation => animationController;

        /// <summary>PlayerController 접근 (State 클래스에서 입력 상태 읽기용)</summary>
        public PlayerController Input => playerInput;

        /// <summary>FSM에 상태 전환 요청 (State 클래스 전용)</summary>
        public CharacterStateMachine StateMachine => stateMachine;

        /// <summary>현재 이동 방향 벡터 (PlayerController가 매 프레임 갱신)</summary>
        public Vector3 CurrentMoveDirection { get; private set; }

        // ──────────────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            motor = GetComponent<PhysicsMotor>();
            playerInput = GetComponent<PlayerController>();
            stateMachine = new CharacterStateMachine();

            RegisterStates();

            // FSM 전환 이벤트를 외부로 중계
            stateMachine.OnTransition += (stateId) => OnStateChanged?.Invoke(stateId);
        }

        private void Start()
        {
            // 게임 시작 시 RunState로 초기화
            stateMachine.Initialize(StateId.Run);
        }

        private void Update()
        {
            // 1) 시선 처리 (CameraRoot 회전)
            if (cameraController != null)
            {
                Vector2 look = playerInput.LookInput;
                cameraController.Look(look.x, look.y, transform);
            }

            // 2) 매 프레임 현재 상태의 Tick 호출
            stateMachine.Tick(Time.deltaTime);
        }

        // ──────────────────────────────────────────────
        // 상태 등록
        // ──────────────────────────────────────────────

        private void RegisterStates()
        {
            stateMachine.RegisterState(StateId.Run, new States.RunState(this));
            stateMachine.RegisterState(StateId.Airborne, new States.AirborneState(this));
            stateMachine.RegisterState(StateId.FreeFall, new States.FreeFallState(this));
            stateMachine.RegisterState(StateId.QuadRecovery, new States.QuadRecoveryState(this));
            stateMachine.RegisterState(StateId.WallRun, new States.WallRunState(this));
            stateMachine.RegisterState(StateId.Ceiling, new States.CeilingState(this));
            stateMachine.RegisterState(StateId.Slide, new States.SlideState(this));
        }

        // ──────────────────────────────────────────────
        // 외부 API (PlayerController → Facade)
        // ──────────────────────────────────────────────

        /// <summary>이동 방향 갱신. PlayerController의 Update에서 매 프레임 호출됩니다.</summary>
        public void RequestMove(Vector3 direction)
        {
            CurrentMoveDirection = direction;
        }

        /// <summary>
        /// 점프 명령. PlayerController의 OnJump 콜백에서 즉시 호출됩니다.
        /// ★ 파이프라인: OnJump 콜백 → Facade.RequestJump() → StateMachine.HandleJump() → State.HandleJump()
        /// </summary>
        public void RequestJump()
        {
            Debug.Log("[CharacterFacade] RequestJump() → StateMachine.HandleJump()");
            stateMachine.HandleJump();
        }

        /// <summary>
        /// 슬라이드 명령. PlayerController의 OnSlide 콜백에서 즉시 호출됩니다.
        /// </summary>
        public void RequestSlide()
        {
            stateMachine.HandleSlide();
        }

        // ──────────────────────────────────────────────
        // 외부 API (Map System → Facade)
        // ──────────────────────────────────────────────

        /// <summary>트리거 충돌 처리. EventTriggers가 호출합니다.</summary>
        public void HandleTrigger(string triggerType)
        {
            switch (triggerType)
            {
                case "FreeFall":
                    stateMachine.ChangeState(StateId.FreeFall);
                    OnActionPerformed?.Invoke("FreeFallEnter");
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // 이벤트 방송 (State → 외부)
        // ──────────────────────────────────────────────

        /// <summary>콤보 배수를 외부에 방송합니다</summary>
        public void BroadcastComboUpdate(float multiplier)
        {
            OnComboUpdate?.Invoke(multiplier);
        }

        /// <summary>액션 수행을 외부에 방송합니다 (점수 계산용)</summary>
        public void BroadcastAction(string actionName)
        {
            OnActionPerformed?.Invoke(actionName);
        }
    }
}
