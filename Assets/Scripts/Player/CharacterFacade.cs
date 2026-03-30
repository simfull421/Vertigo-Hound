// ============================================================================
// CharacterFacade.cs — Facade 패턴 (외부 API 단일 창구)
// 외부 시스템(Map, UI, Tutorial)은 이 클래스를 통해서만 플레이어와 소통합니다.
// 내부적으로 StateMachine, PhysicsMotor, CameraController, AnimationController에 위임합니다.
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
        [Tooltip("1인칭 카메라 컨트롤러")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("1인칭 애니메이션 컨트롤러")]
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

        /// <summary>PhysicsMotor 직접 접근 (State 클래스 전용)</summary>
        public PhysicsMotor Motor => motor;

        /// <summary>CameraController 직접 접근 (State 클래스 전용)</summary>
        public CameraController Camera => cameraController;

        /// <summary>AnimationController 직접 접근 (State 클래스 전용)</summary>
        public AnimationController Animation => animationController;

        /// <summary>PlayerController 직접 접근 (State 클래스에서 입력 읽기용)</summary>
        public PlayerController Input => playerInput;

        /// <summary>FSM에 상태 전환 요청 (State 클래스 전용)</summary>
        public CharacterStateMachine StateMachine => stateMachine;

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
            // 매 프레임 현재 상태의 Tick 호출
            stateMachine.Tick(Time.deltaTime);
        }

        // ──────────────────────────────────────────────
        // 상태 등록 (모든 State를 FSM에 등록)
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

        /// <summary>이동 요청. 현재 상태에 따라 처리 방식이 달라집니다.</summary>
        public void RequestMove(Vector3 direction)
        {
            // 이동은 각 State의 Tick에서 Motor를 통해 처리하므로
            // 여기서는 방향 벡터를 저장만 합니다.
            CurrentMoveDirection = direction;
        }

        /// <summary>점프 요청</summary>
        public void RequestJump()
        {
            // 현재 상태에 따라 FSM이 알아서 처리
            // RunState: 바닥 점프 → AirborneState
            // WallRunState: 벽 차기 → AirborneState
            // CeilingState: 중력 복구 → AirborneState(또는 RunState)
            // 그 외 상태에서는 무시
        }

        /// <summary>슬라이드 요청</summary>
        public void RequestSlide()
        {
            // RunState에서만 유효
        }

        // ──────────────────────────────────────────────
        // 공유 데이터 (State 클래스들이 접근)
        // ──────────────────────────────────────────────

        /// <summary>현재 이동 방향 벡터 (PlayerController가 매 프레임 갱신)</summary>
        public Vector3 CurrentMoveDirection { get; private set; }

        // ──────────────────────────────────────────────
        // 외부 API (Map System → Facade)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 트리거 충돌을 처리합니다. EventTriggers가 호출합니다.
        /// </summary>
        /// <param name="triggerType">충돌한 트리거의 종류</param>
        public void HandleTrigger(string triggerType)
        {
            switch (triggerType)
            {
                case "FreeFall":
                    stateMachine.ChangeState(StateId.FreeFall);
                    OnActionPerformed?.Invoke("FreeFallEnter");
                    break;
                // 추가 트리거 타입은 여기에 확장
            }
        }

        // ──────────────────────────────────────────────
        // 콤보 배수 방송 (ScoreManager 연동)
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
