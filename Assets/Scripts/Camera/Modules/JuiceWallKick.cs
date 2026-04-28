using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class JuiceWallKick : IJuiceModule
{
    [Header("Wall Kick Juice")]
    public float kickPitchAngle = -15f; 
    
    public bool IsActive => _routine != null;
    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }
    public float FovOffset => 0f;

    private CameraJuiceController _hub;
    private Coroutine _routine;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    public void TriggerWallKick(float duration)
    {
        if (_routine != null)
        {
            _hub.StopCoroutine(_routine);
            _routine = null;
        }
        
        _hub.RegisterModule(this);
        _routine = _hub.StartCoroutine(Routine(duration));
    }

    private IEnumerator Routine(float duration)
    {
        float elapsed = 0f;
        float halfDuration = duration * 0.3f;
        float returnDuration = duration * 0.7f;

        // 젖혀지기
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            t = Mathf.Sin(t * Mathf.PI * 0.5f);
            
            RotOffset = new Vector3(Mathf.Lerp(0f, kickPitchAngle, t), 0f, 0f);
            yield return null;
        }

        elapsed = 0f;
        // 원래대로 돌아오기
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            t = 1f - Mathf.Cos(t * Mathf.PI * 0.5f); 

            RotOffset = new Vector3(Mathf.Lerp(kickPitchAngle, 0f, t), 0f, 0f);
            yield return null;
        }

        RotOffset = Vector3.zero;
        _routine = null;
        _hub.UnregisterModule(this);
    }
}
