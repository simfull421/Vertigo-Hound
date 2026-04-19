using UnityEngine;

/// <summary>
/// 월드 모델(애니메이터가 붙은 자식 오브젝트)에서 발생하는 애니메이션 이벤트를 
/// 부모인 PlayerController로 직접 전달하는 스크립트입니다.
/// </summary>
public class AnimationEventForwarder : MonoBehaviour
{
    private PlayerController _player;

    private void Awake()
    {
        // 런타임 성능을 위해 SendMessage 대신 직접 참조를 캐싱합니다.
        _player = GetComponentInParent<PlayerController>();
        
        if (_player == null)
        {
            Debug.LogError($"[AnimationEventForwarder] '{gameObject.name}'의 부모 중 PlayerController를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 애니메이션 클립에서 호출될 이벤트 함수입니다. (Event Name: PlayFootstep)
    /// </summary>
    /// <param name="side">"Left" 또는 "Right" 문자열 파라미터</param>
    public void PlayFootstep(string side)
    {
        if (_player != null)
        {
            _player.OnPlayerFootstep(side);
        }
    }
}
