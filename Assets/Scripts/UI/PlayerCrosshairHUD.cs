using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PlayerCrosshairHUD : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerController player;
    
    [Header("Reticle Colors")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.4f);
    public Color vaultColor = new Color(1f, 0f, 0f, 1.0f); // 명백한 Vault 상징 (Red)
    public Color reboundColor = new Color(0f, 1f, 1f, 1.0f); // 튕겨나가는 Rebound 상징 (Cyan)
    
    [Header("Reticle Scale")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 vaultScale = new Vector3(1.4f, 1.4f, 1.4f);
    
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

        // 리바운드를 최우선으로 표시, 그 다음이 Vault
        bool canRebound = player.wallRebound != null && player.wallRebound.CanRebound;
        bool canVault = player.vault.CanVault && !canRebound;

        Color targetColor = normalColor;
        Vector3 targetScale = normalScale;

        if (canRebound)
        {
            targetColor = reboundColor;
            targetScale = vaultScale; // 리바운드 시에도 확장 연출 동일하게
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
