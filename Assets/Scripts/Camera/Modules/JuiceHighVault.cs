using UnityEngine;
using System.Collections;

/// <summary>
/// 하이 볼트(좁은 틈새 통과) 시의 전용 카메라 연출 모듈.
/// 고개를 깊게 숙이며 몸을 낮추는 연출을 담당합니다.
/// </summary>
[System.Serializable]
public class JuiceHighVault : IJuiceModule
{
    [Header("Values")]
    public float targetPitch = -110f;
    public float targetHeightDip = -0.4f;

    public bool IsActive { get; private set; }
    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }
    public float FovOffset { get; private set; }

    private Coroutine _routine;

    public void Trigger(CameraJuiceController cjc, float duration)
    {
        if (_routine != null) cjc.StopCoroutine(_routine);
        
        IsActive = true;
        cjc.RegisterModule(this);
        _routine = cjc.StartCoroutine(Routine(cjc, duration));
    }

    private IEnumerator Routine(CameraJuiceController cjc, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            float tiltAmount = Mathf.Sin(t * Mathf.PI); 
            
            RotOffset = new Vector3(targetPitch * tiltAmount, 0f, 0f);
            PosOffset = new Vector3(0f, targetHeightDip * tiltAmount, 0f);
            
            yield return null;
        }
        
        RotOffset = Vector3.zero;
        PosOffset = Vector3.zero;
        
        IsActive = false;
        cjc.UnregisterModule(this);
        _routine = null;
    }
}
