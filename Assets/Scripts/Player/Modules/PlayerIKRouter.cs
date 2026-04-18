using UnityEngine;
using System;

/// <summary>
/// Animator 컴포넌트가 부착된 게임 오브젝트에서 OnAnimatorIK 이벤트를 수신하여 POCO 클래스로 전달하는 라우터
/// </summary>
public class PlayerIKRouter : MonoBehaviour
{
    public Action<int> onIK;

    private void OnAnimatorIK(int layerIndex)
    {
        onIK?.Invoke(layerIndex);
    }
}
