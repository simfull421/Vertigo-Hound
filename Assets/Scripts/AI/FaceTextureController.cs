using UnityEngine;

/// <summary>
/// AI의 얼굴(투명 Quad) 텍스처를 상황에 맞게 교체하여 표정을 연출하는 컨트롤러입니다.
/// </summary>
public class FaceTextureController : MonoBehaviour
{
    [Tooltip("얼굴을 렌더링하는 MeshRenderer (Quad)")]
    public MeshRenderer faceRenderer;
    
    [Tooltip("0: 기본, 1: 비웃음(도발), 2: 화남 등 인덱스별 텍스처")]
    public Texture2D[] faceTextures;
    
    [Tooltip("쉐이더의 텍스처 프로퍼티 이름 (URP는 _BaseMap, Standard는 _MainTex)")]
    public string texturePropertyName = "_BaseMap";

    private MaterialPropertyBlock _propBlock;

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 인덱스에 해당하는 텍스처로 얼굴 표정을 교체합니다.
    /// </summary>
    public void SetFace(int index)
    {
        if (faceRenderer == null || faceTextures == null || faceTextures.Length == 0) return;
        if (index < 0 || index >= faceTextures.Length) return;

        // MaterialPropertyBlock을 사용하여 매테리얼 인스턴스 복제를 방지하고 가볍게 교체
        faceRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetTexture(texturePropertyName, faceTextures[index]);
        faceRenderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// 기본 표정(인덱스 0)으로 되돌립니다.
    /// </summary>
    public void ResetFace()
    {
        SetFace(0);
    }
}
