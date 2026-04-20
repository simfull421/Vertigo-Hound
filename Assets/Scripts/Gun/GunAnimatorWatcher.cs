using UnityEngine;

/// <summary>
/// [진단 전용 스크립트] GunUpper 오브젝트에 직접 부착하세요.
/// 런타임에서 이 Animator의 컨트롤러/파라미터/Speed가 외부에서 변조되는지 실시간으로 감시합니다.
/// 버그 원인을 찾은 후에는 이 컴포넌트를 제거하면 됩니다.
/// </summary>
public class GunAnimatorWatcher : MonoBehaviour
{
    private Animator _anim;
    private RuntimeAnimatorController _lastController;
    private float _lastSpeed;
    private string _lastStateName;

    // 파라미터 감시용: 처음에 발견된 파라미터를 기억합니다
    private AnimatorControllerParameter[] _params;

    [Header("진단 옵션")]
    [Tooltip("매 프레임 로그를 찍으면 너무 많으므로, 변화가 있을 때만 찍습니다.")]
    public bool logOnlyOnChange = true;
    [Tooltip("false로 하면 매 프레임 현재 상태를 찍습니다 (매우 많은 로그 주의)")]
    public bool silentIfNoChange = true;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        if (_anim == null)
        {
            Debug.LogError("[GunAnimatorWatcher] Animator 컴포넌트를 찾을 수 없습니다! GunUpper에 붙여야 합니다.");
            enabled = false;
            return;
        }

        _lastController = _anim.runtimeAnimatorController;
        _lastSpeed = _anim.speed;
        Debug.Log($"[GunWatcher] ▶ 감시 시작\n" +
                  $"  오브젝트: {gameObject.name}\n" +
                  $"  초기 컨트롤러: {(_lastController != null ? _lastController.name : "NULL !!!")}\n" +
                  $"  초기 Speed: {_lastSpeed:F3}\n" +
                  $"  Avatar: {(_anim.avatar != null ? _anim.avatar.name : "NULL")}\n" +
                  $"  IsHuman: {(_anim.isHuman)}\n" +
                  $"  부모: {(transform.parent != null ? transform.parent.name : "루트 없음")}");
    }

    void Start()
    {
        // Start 시점: Awake 이후 한 프레임 뒤에 컨트롤러가 바뀌는 경우를 잡기 위함
        if (_anim.runtimeAnimatorController != _lastController)
        {
            Debug.LogError($"[GunWatcher] ⛔ Start() 시점에 컨트롤러가 교체됨!\n" +
                           $"  Awake 시: {(_lastController != null ? _lastController.name : "NULL")}\n" +
                           $"  Start 시: {(_anim.runtimeAnimatorController != null ? _anim.runtimeAnimatorController.name : "NULL")}\n" +
                           $"  → 이것은 Awake와 Start 사이에 외부에서 SetActive 또는 Initialize 호출이 있었다는 뜻입니다.");
            _lastController = _anim.runtimeAnimatorController;
        }

        // 파라미터 목록 스냅샷
        if (_anim.runtimeAnimatorController != null)
        {
            _params = _anim.parameters;
            if (_params.Length > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"[GunWatcher] 초기 파라미터 목록 ({_params.Length}개):");
                foreach (var p in _params)
                    sb.AppendLine($"  - {p.name} ({p.type})");
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.Log("[GunWatcher] 이 Animator에는 파라미터가 0개입니다. 정상입니다(Pistol Idle Only).");
            }
        }

        // 현재 재생 중인 상태 이름 기록
        if (_anim.runtimeAnimatorController != null)
        {
            var stateInfo = _anim.GetCurrentAnimatorStateInfo(0);
            _lastStateName = stateInfo.IsName("") ? "(알 수 없음)" : AnimatorUtility.GetStateName(_anim, 0);
        }
    }

    void Update()
    {
        if (_anim == null) return;

        bool changed = false;

        // ─── 1. 컨트롤러 교체 감시 ───────────────────────────────────
        var currentController = _anim.runtimeAnimatorController;
        if (currentController != _lastController)
        {
            Debug.LogError($"[GunWatcher] ⛔ [프레임 {Time.frameCount}] runtimeAnimatorController 교체 감지!\n" +
                           $"  이전: {(_lastController != null ? _lastController.name : "NULL")}\n" +
                           $"  현재: {(currentController != null ? currentController.name : "NULL")}\n" +
                           $"  → 이 시점의 Call Stack을 확인하세요! (Unity Profiler 또는 break point 사용)");
            _lastController = currentController;
            changed = true;
        }

        // ─── 2. Speed 변화 감시 ──────────────────────────────────────
        float currentSpeed = _anim.speed;
        if (Mathf.Abs(currentSpeed - _lastSpeed) > 0.001f)
        {
            Debug.LogWarning($"[GunWatcher] ⚠️ [프레임 {Time.frameCount}] animator.speed 변화 감지!\n" +
                             $"  이전: {_lastSpeed:F4} → 현재: {currentSpeed:F4}\n" +
                             $"  → PlayerAnimatorHandler가 World Model Animator가 아닌 Gun Animator의 speed를 건드리고 있을 가능성이 있습니다!");
            _lastSpeed = currentSpeed;
            changed = true;
        }

        // ─── 3. 파라미터 주입 감시 ───────────────────────────────────
        if (_params != null && _params.Length > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool paramChanged = false;

            foreach (var p in _params)
            {
                string val = p.type switch
                {
                    AnimatorControllerParameterType.Float   => _anim.GetFloat(p.name).ToString("F3"),
                    AnimatorControllerParameterType.Int     => _anim.GetInteger(p.name).ToString(),
                    AnimatorControllerParameterType.Bool    => _anim.GetBool(p.name).ToString(),
                    AnimatorControllerParameterType.Trigger => "(Trigger)",
                    _ => "?"
                };
                sb.AppendLine($"  {p.name} ({p.type}): {val}");
                paramChanged = true;
            }

            if (paramChanged && (!silentIfNoChange || changed))
            {
                Debug.Log($"[GunWatcher] [프레임 {Time.frameCount}] 파라미터 현재값:\n{sb}");
            }
        }

        // ─── 4. 현재 재생 State 이름 감시 ────────────────────────────
        if (currentController != null)
        {
            string currentState = AnimatorUtility.GetStateName(_anim, 0);
            if (currentState != _lastStateName)
            {
                Debug.LogWarning($"[GunWatcher] 🎬 [프레임 {Time.frameCount}] 애니메이션 State 변경!\n" +
                                 $"  이전: {_lastStateName}\n" +
                                 $"  현재: {currentState}\n" +
                                 $"  → 이 State가 Walk/Run/Idle Blend Tree라면, 외부에서 파라미터나 컨트롤러가 주입된 것입니다!");
                _lastStateName = currentState;
                changed = true;
            }
        }

        // ─── 5. isHuman + Avatar 감시 ────────────────────────────────
        // Humanoid 충돌이 원인이면 이 경고가 의미 있음
        if (!silentIfNoChange && Time.frameCount % 60 == 0) // 60프레임마다 한 번
        {
            Debug.Log($"[GunWatcher] 주기 보고 [프레임 {Time.frameCount}]\n" +
                      $"  isHuman: {_anim.isHuman}\n" +
                      $"  Avatar: {(_anim.avatar != null ? _anim.avatar.name : "NULL")}\n" +
                      $"  Controller: {(currentController != null ? currentController.name : "NULL")}\n" +
                      $"  Speed: {currentSpeed:F3}");
        }
    }

    void OnDestroy()
    {
        Debug.Log("[GunWatcher] 진단 감시 종료.");
    }
}

/// <summary>
/// Animator State 이름을 가져오는 유틸리티 (내부용)
/// </summary>
internal static class AnimatorUtility
{
    public static string GetStateName(Animator anim, int layer)
    {
        if (anim == null || !anim.isActiveAndEnabled) return "(비활성)";
        try
        {
            var info = anim.GetCurrentAnimatorStateInfo(layer);
            // Unity는 State 이름을 해시로만 관리하므로, nameHash를 문자열로 직접 변환 불가
            // 대신 클립 이름을 통해 간접 확인
            var clips = anim.GetCurrentAnimatorClipInfo(layer);
            if (clips.Length > 0)
                return clips[0].clip != null ? clips[0].clip.name : $"Hash:{info.shortNameHash}";
            return $"Hash:{info.shortNameHash} (클립 없음)";
        }
        catch
        {
            return "(조회 실패)";
        }
    }
}
