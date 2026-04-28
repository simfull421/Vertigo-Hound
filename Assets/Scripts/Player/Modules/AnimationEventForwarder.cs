using UnityEngine;

/// <summary>
/// 자식 모델의 Animator에서 발생하는 Animation Event를 부모의 PlayerController 본체로 전달해주는 브릿지(Forwarder) 스크립트입니다.
/// </summary>
public class AnimationEventForwarder : MonoBehaviour
{
    private PlayerController _hub;

    private void Start()
    {
        // 최상단 부모에서 PlayerController 허브를 찾아 캐싱합니다. (Find나 GetComponent 매 프레임 호출 방지)
        _hub = GetComponentInParent<PlayerController>();
        
        if (_hub == null)
        {
            Debug.LogWarning($"[{gameObject.name}] AnimationEventForwarder: 부모에서 PlayerController를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 애니메이션 클립에서 "PlayFootstep" 이벤트가 발생할 때 호출됩니다.
    /// </summary>
    /// <param name="side">이벤트에서 넘겨주는 파라미터 (예: "Left", "Right")</param>
    public void PlayFootstep(string side)
    {
        if (_hub != null)
        {
            _hub.PlayFootstep(side);
        }
    }

    /// <summary>
    /// 애니메이션 클립에서 발이 뻗어질 때 (Kick 타격 타이밍) 호출됩니다.
    /// </summary>
    public void OnKickHitEvent()
    {
        if (_hub != null && _hub.animatorHandler != null)
        {
            _hub.animatorHandler.OnKickHitEvent();
        }
    }
}
