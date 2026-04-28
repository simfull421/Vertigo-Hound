using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PlayerCrosshairHUD : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerController player;
    
    [Header("Reticle Colors")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.4f);
    public Color vaultColor = new Color(1f, 0f, 0f, 1.0f); // 일반 Vault (Red)
    public Color highVaultColor = new Color(0f, 1f, 1f, 1.0f); // 하이 Vault (Cyan/하늘색)
  
    [Header("Reticle Scale")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 vaultScale = new Vector3(1.4f, 1.4f, 1.4f);
    public Vector3 highVaultScale = new Vector3(1.8f, 1.8f, 1.8f); // 틈새는 더 크게 강조
    
    [Header("Animation")]
    public float lerpSpeed = 15f;

    private Image _crosshairImage;

    void Awake()
    {
        _crosshairImage = GetComponent<Image>();
    }

    void Update()
    {
        if (player == null || player.vault == null || _crosshairImage == null) return;

        bool canVault = player.vault.CanVault;
        bool canHighVault = player.vault.CanHighVault; // 하이 볼트 상태 가져오기

        Color targetColor = normalColor;
        Vector3 targetScale = normalScale;

        // 우선순위: 하이 볼트 > 일반 볼트
        if (canHighVault)
        {
            targetColor = highVaultColor;
            targetScale = highVaultScale;
        }
        else if (canVault)
        {
            targetColor = vaultColor;
            targetScale = vaultScale;
        }

        // 시각적 피드백 보간
        _crosshairImage.color = Color.Lerp(_crosshairImage.color, targetColor, Time.deltaTime * lerpSpeed);
        _crosshairImage.rectTransform.localScale = Vector3.Lerp(_crosshairImage.rectTransform.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }
}