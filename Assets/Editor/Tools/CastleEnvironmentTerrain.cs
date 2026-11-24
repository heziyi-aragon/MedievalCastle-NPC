using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedievalCastle.Editor
{
    public static class CastleEnvironmentTerrain
    {
        private const string SceneBackup = "Assets/Scenes/Backup/Castle_Exterior_BeforeEnvironment.unity";
        private const string TerrainBackup = "Assets/Scenes/Backup/New Terrain_BeforeEnvironment.asset";

        [MenuItem("Tools/Castle Environment/3 - Sculpt Terraces and Road", priority = 202)]
        public static void SculptTerracesAndRoad()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            Terrain terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (castle == null || terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Castle or Terrain is missing from Castle_Exterior.");
            }

            CreateBackups(terrain);
            Bounds bounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            TerrainData data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Sculpt castle environment terraces and road");
            RestoreTerrainFromBackup(data);
            SculptHeights(terrain, data, bounds);
            PaintSoil(terrain, data, bounds);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CastleEnvironmentBuilder.BuildFirstPass();
            Debug.Log("CASTLE_ENVIRONMENT_TERRAIN_OK: market/training terraces sculpted and entrance road painted.");
        }

        private static void CreateBackups(Terrain terrain)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBackup))
            {
                AssetDatabase.CopyAsset("Assets/Scenes/Castle_Exterior.unity", SceneBackup);
            }

            string terrainPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            if (!AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackup))
            {
                AssetDatabase.CopyAsset(terrainPath, TerrainBackup);
            }
        }

        private static void RestoreTerrainFromBackup(TerrainData target)
        {
            TerrainData backup = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackup);
            if (backup == null || backup.heightmapResolution != target.heightmapResolution ||
                backup.alphamapResolution != target.alphamapResolution)
            {
                throw new InvalidOperationException("Environment terrain backup is missing or incompatible.");
            }

            int heightResolution = target.heightmapResolution;
            target.SetHeights(0, 0, backup.GetHeights(0, 0, heightResolution, heightResolution));
            int alphaResolution = target.alphamapResolution;
            target.SetAlphamaps(0, 0, backup.GetAlphamaps(0, 0, alphaResolution, alphaResolution));
            target.SyncHeightmap();
        }

        private static void SculptHeights(Terrain terrain, TerrainData data, Bounds castleBounds)
        {
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;
            Vector3 market = CastleEnvironmentLayout.MarketCenter(castleBounds);
            Vector3 training = CastleEnvironmentLayout.TrainingCenter(castleBounds);
            Vector3 guard = CastleEnvironmentLayout.GuardCenter(castleBounds);
            float marketTarget = NormalizeHeight(terrain.SampleHeight(market) + terrainPosition.y, terrainPosition.y, terrainSize.y);
            float trainingTarget = NormalizeHeight(terrain.SampleHeight(training) + terrainPosition.y, terrainPosition.y, terrainSize.y);
            float guardTarget = NormalizeHeight(terrain.SampleHeight(guard) + terrainPosition.y, terrainPosition.y, terrainSize.y);
            Vector3 gate = CastleEnvironmentLayout.GateAnchor(castleBounds);
            Vector2 roadStart = new Vector2(gate.x, gate.z);
            Vector2 roadEnd = roadStart + Vector2.up * 40f;
            Vector3 roadEndWorld = new Vector3(roadEnd.x, 0f, roadEnd.y);
            float roadEndTarget = NormalizeHeight(terrain.SampleHeight(roadEndWorld) + terrainPosition.y, terrainPosition.y, terrainSize.y);

            for (int z = 0; z < resolution; z++)
            {
                float worldZ = terrainPosition.z + z / (resolution - 1f) * terrainSize.z;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = terrainPosition.x + x / (resolution - 1f) * terrainSize.x;
                    Vector2 point = new Vector2(worldX, worldZ);
                    float value = heights[z, x];
                    value = BlendTerrace(value, marketTarget, point, new Vector2(market.x, market.z), new Vector2(7.5f, 6f), 9f);
                    value = BlendTerrace(value, trainingTarget, point, new Vector2(training.x, training.z), new Vector2(8.5f, 7f), 10f);
                    value = BlendTerrace(value, guardTarget, point, new Vector2(guard.x, guard.z), new Vector2(6.5f, 3f), 4f);

                    float roadMask = CastleEnvironmentLayout.RoadMask(point, roadStart, roadEnd, 2.5f, 4.5f);
                    if (roadMask > 0f)
                    {
                        float t = Mathf.InverseLerp(roadStart.y, roadEnd.y, worldZ);
                        float roadTarget = Mathf.Lerp(guardTarget, roadEndTarget, Mathf.SmoothStep(0f, 1f, t));
                        value = Mathf.Lerp(value, roadTarget, roadMask * 0.65f);
                    }

                    heights[z, x] = Mathf.Clamp01(value);
                }
            }

            data.SetHeights(0, 0, heights);
            data.SyncHeightmap();
        }

        private static void PaintSoil(Terrain terrain, TerrainData data, Bounds castleBounds)
        {
            if (data.terrainLayers.Length < 2)
            {
                throw new InvalidOperationException("Terrain needs grass and soil layers before painting the entrance road.");
            }

            int resolution = data.alphamapResolution;
            float[,,] maps = data.GetAlphamaps(0, 0, resolution, resolution);
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;
            Vector3 gate = CastleEnvironmentLayout.GateAnchor(castleBounds);
            Vector3 market = CastleEnvironmentLayout.MarketCenter(castleBounds);
            Vector3 training = CastleEnvironmentLayout.TrainingCenter(castleBounds);
            Vector3 guard = CastleEnvironmentLayout.GuardCenter(castleBounds);
            Vector2 roadStart = new Vector2(gate.x, gate.z);
            Vector2 roadEnd = roadStart + Vector2.up * 40f;

            for (int z = 0; z < resolution; z++)
            {
                float worldZ = terrainPosition.z + z / (resolution - 1f) * terrainSize.z;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = terrainPosition.x + x / (resolution - 1f) * terrainSize.x;
                    Vector2 point = new Vector2(worldX, worldZ);
                    float road = CastleEnvironmentLayout.RoadMask(point, roadStart, roadEnd, 2.5f, 2f);
                    float marketMask = CastleEnvironmentLayout.SmoothRectangleMask(point, new Vector2(market.x, market.z), new Vector2(7.5f, 6f), 2f);
                    float trainingMask = CastleEnvironmentLayout.SmoothRectangleMask(point, new Vector2(training.x, training.z), new Vector2(8.5f, 7f), 2f);
                    float guardMask = CastleEnvironmentLayout.SmoothRectangleMask(point, new Vector2(guard.x, guard.z), new Vector2(6.5f, 3f), 1.5f);
                    float mask = Mathf.Max(road, Mathf.Max(marketMask * 0.75f, Mathf.Max(trainingMask * 0.75f, guardMask * 0.8f)));
                    if (mask <= 0f)
                    {
                        continue;
                    }

                    float oldSoil = maps[z, x, 1];
                    float newSoil = Mathf.Lerp(oldSoil, 0.88f, mask);
                    float remainingOld = Mathf.Max(0.0001f, 1f - oldSoil);
                    float remainingNew = 1f - newSoil;
                    for (int layer = 0; layer < data.terrainLayers.Length; layer++)
                    {
                        if (layer != 1)
                        {
                            maps[z, x, layer] *= remainingNew / remainingOld;
                        }
                    }
                    maps[z, x, 1] = newSoil;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        private static float BlendTerrace(
            float current,
            float target,
            Vector2 point,
            Vector2 center,
            Vector2 halfSize,
            float feather)
        {
            float mask = CastleEnvironmentLayout.SmoothRectangleMask(point, center, halfSize, feather);
            return Mathf.Lerp(current, target, mask);
        }

        private static float NormalizeHeight(float worldHeight, float terrainY, float terrainHeight)
        {
            return Mathf.Clamp01((worldHeight - terrainY) / terrainHeight);
        }
    }
}
