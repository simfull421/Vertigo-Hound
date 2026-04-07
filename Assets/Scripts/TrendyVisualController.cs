using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TrendyVisualController : MonoBehaviour
{
    [Header("Volume Settings")]
    public Volume globalVolume;
    public Camera mainCamera;

    [Header("FOV Settings")]
    public float normalFOV = 90f;
    public float dashFOV = 115f;
    public float fovSpeed = 10f;

    private Vignette vignette;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;

    private Color normalVignette = Color.blue;
    private Color dashReadyVignette = Color.red; 

    // 인터페이스를 통한 의존성
    private IMomentumProvider momentumProvider;
    private IInputProvider inputProvider;

    // 외부(PlayerController)에서 의존성을 주입해주는 초기화 함수
    public void Initialize(IMomentumProvider momentum, IInputProvider input)
    {
        this.momentumProvider = momentum;
        this.inputProvider = input;
    }

    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out chromatic);
            globalVolume.profile.TryGet(out lensDistortion);
        }
    }

    void Update()
    {
        // 의존성이 주입되지 않았다면 실행하지 않음
        if (momentumProvider == null || inputProvider == null) return;

        HandleFOV();
        HandlePostProcessing();
    }

    private void HandleFOV()
    {
        bool isDashing = inputProvider.DashHeld && momentumProvider.Value > 10f;
        float targetFOV = isDashing ? dashFOV : normalFOV;
        
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, fovSpeed * Time.deltaTime);
    }

    private void HandlePostProcessing()
    {
        float mnt = momentumProvider.Value;
        float normMnt = momentumProvider.NormalizedValue; // 0 ~ 1

        if (chromatic != null)
        {
            chromatic.active = mnt > 50f;
            chromatic.intensity.value = Mathf.Lerp(0f, 1f, (mnt - 50f) / 50f);
        }

        if (lensDistortion != null)
        {
            lensDistortion.active = mnt > 60f;
            lensDistortion.intensity.value = Mathf.Lerp(0f, -0.3f, (mnt - 60f) / 40f);
        }

        if (vignette != null)
        {
            vignette.active = mnt > 30f;
            vignette.intensity.value = Mathf.Lerp(0f, 0.45f, (mnt - 30f) / 70f);

            Color targetColor = (mnt > 90f) ? dashReadyVignette : normalVignette;
            vignette.color.value = Color.Lerp(vignette.color.value, targetColor, 10f * Time.deltaTime);
        }
    }
}