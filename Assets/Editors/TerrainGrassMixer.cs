using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TerrainGrassMixer : EditorWindow
{
    private float slopeLimit = 30f;
    private float noiseScale = 15f;
    private float densityMultiplier = 1f;
    private int maxGrassPerPatch = 5; // 좀 더 촘촘하게 5로 상향

    [MenuItem("Tools/Map Generator/Auto Grass Mixer")]
    public static void ShowWindow() => GetWindow<TerrainGrassMixer>("Auto Grass");

    private void OnGUI()
    {
        GUILayout.Label("잔디 자동 혼합 살포기 (데이터 갱신 강화 버전)", EditorStyles.boldLabel);
        
        slopeLimit = EditorGUILayout.Slider("최대 경사", slopeLimit, 10f, 60f);
        noiseScale = EditorGUILayout.Slider("노이즈 (군집)", noiseScale, 1f, 50f);
        densityMultiplier = EditorGUILayout.Slider("밀도 배율", densityMultiplier, 0.1f, 5f);
        maxGrassPerPatch = EditorGUILayout.IntSlider("패치당 최대수", maxGrassPerPatch, 1, 20);

        if (GUILayout.Button("1딸깍 살포 및 화면 갱신!", GUILayout.Height(40)))
        {
            MixAndPlantGrass();
        }

        if (GUILayout.Button("초기화", GUILayout.Height(20)))
        {
            ClearAllGrass();
        }
    }

    private void MixAndPlantGrass()
    {
        Terrain t = Terrain.activeTerrain;
        if (!t) return;

        TerrainData td = t.terrainData;
        int numDetails = td.detailPrototypes.Length;
        int res = td.detailResolution;
        
        // 실행 취소(Undo) 가능하게 등록
        Undo.RegisterCompleteObjectUndo(td, "Mix Grass");

        List<int[,]> newLayers = new List<int[,]>();
        for (int i = 0; i < numDetails; i++) newLayers.Add(new int[res, res]);

        float seed = Random.Range(0f, 100f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / res;
                float normY = (float)y / res;
                float slope = td.GetSteepness(normX, normY);

                if (slope > slopeLimit) continue;

                float noise = Mathf.PerlinNoise(normX * noiseScale + seed, normY * noiseScale + seed);
                
                if (noise > 0.3f) 
                {
                    int index = Random.Range(0, numDetails);
                    int density = Mathf.RoundToInt(Random.Range(1, maxGrassPerPatch) * densityMultiplier);
                    newLayers[index][y, x] = density;
                }
            }
        }

        for (int i = 0; i < numDetails; i++)
        {
            td.SetDetailLayer(0, 0, i, newLayers[i]);
        }

        // [핵심] 에디터에게 변경사항을 알리고 화면을 새로고침합니다.
        EditorUtility.SetDirty(td);
        t.Flush(); 
        
        Debug.Log($"[완료] {numDetails}종류의 잔디 살포 및 화면 갱신 완료!");
    }

    private void ClearAllGrass()
    {
        Terrain t = Terrain.activeTerrain;
        if (!t) return;
        TerrainData td = t.terrainData;
        for (int i = 0; i < td.detailPrototypes.Length; i++)
        {
            td.SetDetailLayer(0, 0, i, new int[td.detailResolution, td.detailResolution]);
        }
        EditorUtility.SetDirty(td);
        t.Flush();
    }
}