using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
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
    
    [Header("Crosshair Lines")]
    public Image lineUp;
    public Image lineDown;
    public Image lineLeft;
    public Image lineRight;
    public float lineGap = 4f;
    public float lineLength = 10f;
    public float lineThickness = 2f;
    
    [Header("Line Style")]
    public Sprite lineSprite;
    public Image.Type lineImageType = Image.Type.Sliced;
    public bool preserveAspect = true;
    
    [Header("Animation")]
    public float lerpSpeed = 15f;
    public float alphaLerpSpeed = 12f;

    private readonly List<Image> _lineImages = new List<Image>(4);
    private CanvasGroup _canvasGroup;
    private RectTransform _rootRect;

    void Awake()
    {
        CacheComponents();
        ResolveLineImages();
        ApplyLineStyle();
        ApplyLayout();
        InitializeVisualState();
    }

    void OnValidate()
    {
        CacheComponents();
        ResolveLineImages();
        ApplyLineStyle();
        ApplyLayout();
    }

    void Update()
    {
        if (player == null || player.vault == null || _canvasGroup == null) return;

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

        Color targetTint = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        float targetAlpha = targetColor.a;

        // 시각적 피드백 보간
        foreach (var line in _lineImages)
        {
            if (line == null) continue;
            line.color = Color.Lerp(line.color, targetTint, Time.deltaTime * lerpSpeed);
        }

        _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, Time.deltaTime * alphaLerpSpeed);

        if (_rootRect != null)
        {
            _rootRect.localScale = Vector3.Lerp(_rootRect.localScale, targetScale, Time.deltaTime * lerpSpeed);
        }
    }

    private void CacheComponents()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_rootRect == null) _rootRect = GetComponent<RectTransform>();
    }

    private void ResolveLineImages()
    {
        if (lineUp == null) lineUp = FindLineImage("LineUp");
        if (lineDown == null) lineDown = FindLineImage("LineDown");
        if (lineLeft == null) lineLeft = FindLineImage("LineLeft");
        if (lineRight == null) lineRight = FindLineImage("LineRight");

        _lineImages.Clear();
        if (lineUp != null) _lineImages.Add(lineUp);
        if (lineDown != null) _lineImages.Add(lineDown);
        if (lineLeft != null) _lineImages.Add(lineLeft);
        if (lineRight != null) _lineImages.Add(lineRight);
    }

    private Image FindLineImage(string name)
    {
        Transform child = transform.Find(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void ApplyLineStyle()
    {
        Sprite spriteToUse = lineSprite;
        if (spriteToUse == null)
        {
            spriteToUse = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        foreach (var line in _lineImages)
        {
            if (line == null) continue;
            if (spriteToUse != null) line.sprite = spriteToUse;
            line.type = lineImageType;
            line.preserveAspect = preserveAspect;
            line.raycastTarget = false;
        }
    }

    private void ApplyLayout()
    {
        float safeLength = Mathf.Max(0f, lineLength);
        float halfLength = safeLength * 0.5f;
        float offset = Mathf.Max(0f, lineGap) + halfLength;
        float thickness = Mathf.Max(1f, lineThickness);

        ConfigureLine(lineUp, new Vector2(thickness, safeLength), new Vector2(0f, offset));
        ConfigureLine(lineDown, new Vector2(thickness, safeLength), new Vector2(0f, -offset));
        ConfigureLine(lineLeft, new Vector2(safeLength, thickness), new Vector2(-offset, 0f));
        ConfigureLine(lineRight, new Vector2(safeLength, thickness), new Vector2(offset, 0f));
    }

    private void ConfigureLine(Image line, Vector2 size, Vector2 anchoredPosition)
    {
        if (line == null) return;

        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localRotation = Quaternion.identity;
    }

    private void InitializeVisualState()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = normalColor.a;
        }

        Color tint = new Color(normalColor.r, normalColor.g, normalColor.b, 1f);
        foreach (var line in _lineImages)
        {
            if (line == null) continue;
            line.color = tint;
        }

        if (_rootRect != null)
        {
            _rootRect.localScale = normalScale;
        }
    }
}
