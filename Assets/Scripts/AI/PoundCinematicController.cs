using UnityEngine;
using UnityEngine.Playables; // 타임라인 제어용
using DG.Tweening;
using Cinemachine;

public class PoundCinematicController : MonoBehaviour
{
    [Header("Timeline")]
    public PlayableDirector timelineDirector;

    [Header("Real Objects (FSM, Physics 활성화됨)")]
    public GameObject realPlayer;
    [Tooltip("시네마틱 중 숨길 플레이어 비주얼 오브젝트들 (팔, 무기, 모델 등)")]
    public GameObject[] playerVisualsToHide;
    // 인스펙터에서 할당할 필요 없게 숨김
    [HideInInspector] public GameObject dynamicRealAI; 

    [Header("Dummy Objects (애니메이션 전용)")]
    public GameObject dummyPlayer;
    public GameObject dummyAI;

    [Header("Cinematic Cameras")]
    public CinemachineVirtualCamera poundVirtualCamera;
    public CinemachineVirtualCamera aiFleeZoomCam;
    public CameraJuiceController juiceController;

    private EnemyAI _realEnemyAI;

    void Awake()
    {
    }

    void OnEnable()
    {
        // 타임라인 재생이 끝났을 때 호출될 이벤트 구독
        if (timelineDirector != null)
            timelineDirector.stopped += OnTimelineFinished;
    }

    void OnDisable()
    {
        if (timelineDirector != null)
            timelineDirector.stopped -= OnTimelineFinished;
    }

    /// <summary>
    /// 파운딩 시네마틱 돌입 시 호출하여 연출 시작
    /// </summary>
    public void StartPoundCinematic(GameObject attackerAI)
    {
        if (attackerAI == null)
        {
            Debug.LogWarning("[Pounding] StartPoundCinematic 호출 시 attackerAI가 null입니다!");
            return;
        }

        dynamicRealAI = attackerAI;
        _realEnemyAI = dynamicRealAI.GetComponent<EnemyAI>();

        // 1. 더미 위치 동기화 (realAI 대신 dynamicRealAI 사용)
        dummyPlayer.transform.SetPositionAndRotation(realPlayer.transform.position, realPlayer.transform.rotation);
        dummyAI.transform.SetPositionAndRotation(dynamicRealAI.transform.position, dynamicRealAI.transform.rotation);

        // 2. 실제 캐릭터 끄기
        if (playerVisualsToHide != null)
        {
            foreach (var vis in playerVisualsToHide)
                if (vis != null) vis.SetActive(false);
        }
        dynamicRealAI.SetActive(false);

        // 3. 더미 켜기
        dummyPlayer.SetActive(true);
        dummyAI.SetActive(true);

        // 무기 강제 맨손(0) 처리
        PlayerController pc = realPlayer.GetComponent<PlayerController>();
        if (pc != null && pc.animatorHandler != null)
        {
            pc.animatorHandler.ForceSetWeaponType(0);
        }

        if (juiceController != null) juiceController.DisablePlayerCamera();
        if (poundVirtualCamera != null) poundVirtualCamera.Priority = 20;

        // 4. 타임라인 재생
        timelineDirector.Play();
    }

    /// <summary>
    /// 타임라인 시그널(Signal)에서 호출할 함수: 키 탈취 및 도주 로직 실행
    /// </summary>
    public void OnSignalExecuteKeySteal()
    {
        if (dynamicRealAI != null)
        {
            var trollingModule = dynamicRealAI.GetComponent<AITrollingModule>();
            if (trollingModule != null)
            {
                trollingModule.ExecuteKeyStealAndFlee();
            }
            else
            {
                Debug.LogWarning($"[Pounding] dynamicRealAI '{dynamicRealAI.name}'에 AITrollingModule이 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning("[Pounding] OnSignalExecuteKeySteal 호출 시 dynamicRealAI가 null입니다! 타임라인 Signal 시점을 확인하세요.");
        }
    }

    /// <summary>
    /// 타임라인 재생이 완전히 종료되면 자동 호출됨
    /// </summary>
    private void OnTimelineFinished(PlayableDirector director)
    {
        if (director != timelineDirector) return;

        // 1. 더미 끄기
        dummyPlayer.SetActive(false);
        dummyAI.SetActive(false);

        // 실제 캐릭터 위치 복구
        realPlayer.transform.SetPositionAndRotation(dummyPlayer.transform.position, dummyPlayer.transform.rotation);
        if (dynamicRealAI != null)
        {
            dynamicRealAI.transform.SetPositionAndRotation(dummyAI.transform.position, dummyAI.transform.rotation);
            dynamicRealAI.SetActive(true);
        }

        if (playerVisualsToHide != null)
        {
            foreach (var vis in playerVisualsToHide)
                if (vis != null) vis.SetActive(true);
        }

        // 파운딩 카메라 끄기
        if (poundVirtualCamera != null) poundVirtualCamera.Priority = 0;

        // 2. 키 유무 분기 처리 (Node A)
        bool hasKey = DataKeyManager.Instance != null && DataKeyManager.Instance.isKeyHeldByPlayer;

        if (hasKey)
        {
            // 키 뺏기 및 AI 도망 상태 전환
            if (DataKeyManager.Instance != null)
                DataKeyManager.Instance.SetKeyHolder(dynamicRealAI.transform, false);
            if (_realEnemyAI != null) _realEnemyAI.SetState(EnemyAI.EnemyState.Fleeing);

            // 줌인 연출 활성화
            if (aiFleeZoomCam != null) aiFleeZoomCam.Priority = 30;

            // 1.5초 후 복구
            DOVirtual.DelayedCall(1.5f, () => FinishCinematicRoutine()).SetUpdate(true);
        }
        else
        {
            // 키가 없으면 즉시 복구 (AI만 달아남)
            if (_realEnemyAI != null) _realEnemyAI.SetState(EnemyAI.EnemyState.Fleeing);
            FinishCinematicRoutine();
        }
    }

    private void FinishCinematicRoutine()
    {
        if (aiFleeZoomCam != null) aiFleeZoomCam.Priority = 0;

        // 플레이어 면역 10초 부여
        PlayerController pc = realPlayer.GetComponent<PlayerController>();
        if (pc != null) pc.poundingImmunityTimer = 10f;

        // 카메라 및 조작권 하드 리셋 복구
        if (juiceController != null) juiceController.RestoreCameraAndPlayer();

        // AI 컷신 트리거 초기화
        if (dynamicRealAI != null)
        {
            AITrollingModule aiModule = dynamicRealAI.GetComponent<AITrollingModule>();
            if (aiModule != null) aiModule.ResetTrigger();
        }
    }
}
