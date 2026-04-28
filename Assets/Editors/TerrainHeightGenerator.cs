using UnityEngine;
using UnityEditor;

public class TerrainHeightGenerator : EditorWindow
{
    private float noiseScale = 5f;       // 산의 큼직함 (작을수록 거대한 산, 클수록 자잘한 언덕)
    private float heightIntensity = 0.2f; // 산의 최대 높이 비율 (0~1)
    private float offsetX = 0f;
    private float offsetY = 0f;

    [MenuItem("Tools/Map Generator/1. Auto Shape Terrain")]
    public static void ShowWindow() => GetWindow<TerrainHeightGenerator>("Shape Terrain");

    private void OnGUI()
    {
        GUILayout.Label("지형 자동 생성 (펄린 노이즈)", EditorStyles.boldLabel);
        
        noiseScale = EditorGUILayout.Slider("노이즈 스케일 (굴곡)", noiseScale, 1f, 20f);
        heightIntensity = EditorGUILayout.Slider("최대 높이 비율", heightIntensity, 0.01f, 1f);
        
        if (GUILayout.Button("새로운 랜덤 시드 생성"))
        {
            offsetX = Random.Range(0f, 9999f);
            offsetY = Random.Range(0f, 9999f);
            GenerateTerrain();
        }

        if (GUILayout.Button("현재 설정으로 지형 깎기 딸깍!"))
        {
            GenerateTerrain();
        }
        
        EditorGUILayout.Space();
        GUILayout.Label("Tip: 지형 생성 후 Auto Texture Painter를 실행하세요.", EditorStyles.helpBox);
    }

    private void GenerateTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("선택된 Terrain이 없습니다."); return; }

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        
        // 지형 높이 데이터를 담을 2차원 배열
        float[,] heights = new float[res, res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // 펄린 노이즈 좌표 계산
                float xCoord = (float)x / res * noiseScale + offsetX;
                float yCoord = (float)y / res * noiseScale + offsetY;

                // 펄린 노이즈로 0~1 사이의 부드러운 높이값 생성
                float noiseValue = Mathf.PerlinNoise(xCoord, yCoord);
                
                // 실제 지형 높이에 적용
                heights[y, x] = noiseValue * heightIntensity;
            }
        }

        // 터레인에 데이터 덮어쓰기 (실행 취소 지원)
        Undo.RegisterCompleteObjectUndo(td, "Generate Terrain Height");
        td.SetHeights(0, 0, heights);
        
        Debug.Log("지형 깎기 완료! 이제 텍스처를 칠할 차례입니다.");
    }
}