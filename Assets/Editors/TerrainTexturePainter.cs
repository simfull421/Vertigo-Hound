using UnityEngine;
using UnityEditor;

public class TerrainTexturePainter : EditorWindow
{
    [MenuItem("Tools/Map Generator/Auto Texture Painter")]
    public static void ShowWindow() => GetWindow<TerrainTexturePainter>("Auto Painter");

    private float slopeThreshold = 30f; // 이 각도보다 가파르면 바위로 칠함
    private float blendSmoothness = 10f; // 경계선을 얼마나 부드럽게 섞을지

    private void OnGUI()
    {
        GUILayout.Label("지형 텍스처 자동 채색 (1딸깍)", EditorStyles.boldLabel);
        
        slopeThreshold = EditorGUILayout.Slider("절벽 기준 각도", slopeThreshold, 10f, 60f);
        blendSmoothness = EditorGUILayout.Slider("경계선 부드러움", blendSmoothness, 1f, 20f);

        if (GUILayout.Button("자동 칠하기 딸깍!"))
        {
            AutoPaint();
        }
    }

    private void AutoPaint()
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) { Debug.LogError("선택된 Terrain이 없습니다."); return; }
        
        TerrainData td = t.terrainData;

        if (td.terrainLayers.Length < 2)
        {
            Debug.LogWarning("터레인 인스펙터에 최소 2개의 레이어(1번:풀, 2번:바위)를 넣어주세요!");
            return;
        }

        int mapX = td.alphamapWidth;
        int mapY = td.alphamapHeight;
        
        // 텍스처 혼합 비율을 저장할 3차원 배열
        float[,,] splatmapData = new float[mapX, mapY, td.terrainLayers.Length];

        for (int y = 0; y < mapY; y++)
        {
            for (int x = 0; x < mapX; x++)
            {
                // 현재 위치의 경사도(기울기) 계산
                float normX = x * 1.0f / (mapX - 1);
                float normY = y * 1.0f / (mapY - 1);
                float steepness = td.GetSteepness(normX, normY);

                // 각도에 따라 풀과 바위의 비율 계산 (블렌딩)
                float rockWeight = Mathf.InverseLerp(slopeThreshold - blendSmoothness, slopeThreshold + blendSmoothness, steepness);
                float grassWeight = 1.0f - rockWeight;

                // 0번 레이어(풀), 1번 레이어(바위)에 가중치 할당
                splatmapData[y, x, 0] = grassWeight;
                splatmapData[y, x, 1] = rockWeight;
                
                // 만약 레이어가 더 있다면 나머지는 0으로 채움
                for(int i = 2; i < td.terrainLayers.Length; i++) splatmapData[y, x, i] = 0f;
            }
        }

        // 터레인에 데이터 덮어쓰기 (딸깍)
        td.SetAlphamaps(0, 0, splatmapData);
        Debug.Log("자동 채색 완료! 평지는 풀, 절벽은 바위로 칠해졌습니다.");
    }
}