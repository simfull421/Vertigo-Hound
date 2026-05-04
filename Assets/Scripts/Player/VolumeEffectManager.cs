using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // URP 기준 (HDRP나 Built-in이면 네임스페이스가 다름)
using DG.Tweening;

public class VolumeEffectManager : MonoBehaviour
{
    [Header("Post Processing Volume")]
    public Volume globalVolume; // 씬에 있는 Global Volume 할당

    [Header("Hit Effect Settings")]
    public float hitVignetteIntensity = 0.5f;
    public float hitVignetteDuration = 0.3f;
    
    public float hitBlurDistance = 0.1f; // 작을수록 화면이 흐려짐
    public float defaultFocusDistance = 10f; // 평소 초점 거리
    public float hitBlurDuration = 0.4f;

    private Vignette _vignette;
    private DepthOfField _dof;

    void Start()
    {
        // 볼륨 프로파일에서 효과 추출
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _vignette);
            globalVolume.profile.TryGet(out _dof);
        }
    }

    /// <summary>
    /// 타임라인 시그널(Signal) 리시버에 연결할 피격 효과 함수
    /// </summary>
    public void TriggerHitJuice()
    {
        // 1. 화면 가장자리 빡! 까매졌다가 원상복구
        if (_vignette != null)
        {
            DOTween.Kill(_vignette); // 중복 실행 방지
            _vignette.intensity.value = hitVignetteIntensity;
            DOTween.To(() => _vignette.intensity.value, x => _vignette.intensity.value = x, 0f, hitVignetteDuration)
                   .SetEase(Ease.OutQuad);
        }

        // 2. 화면 초점 확 날아갔다가 원상복구 (흐려짐 효과)
        if (_dof != null)
        {
            DOTween.Kill(_dof);
            _dof.focusDistance.value = hitBlurDistance;
            DOTween.To(() => _dof.focusDistance.value, x => _dof.focusDistance.value = x, defaultFocusDistance, hitBlurDuration)
                   .SetEase(Ease.OutQuad);
        }
    }
}