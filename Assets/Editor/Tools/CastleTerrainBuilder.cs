using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedievalCastle.Editor
{
    public static class CastleTerrainBuilder
    {
        private const string GrassDiffuse = "Assets/Textures/Terrain/LeafyGrass/leafy_grass_diff_2k.jpg";
        private const string GrassNormal = "Assets/Textures/Terrain/LeafyGrass/leafy_grass_nor_gl_2k.png";
        private const string SoilDiffuse = "Assets/Art/Textures/Terrain/Soil/dirt_diff_2k.jpg";
        private const string SoilNormal = "Assets/Art/Textures/Terrain/Soil/dirt_nor_gl_2k.png";
        private const string RockDiffuse = "Assets/Art/Textures/Terrain/Rock/rock_01_diff_2k.jpg";
        private const string RockNormal = "Assets/Art/Textures/Terrain/Rock/rock_01_nor_gl_2k.png";
        private const string GrassLayerPath = "Assets/Art/TerrainLayers/TL_Grass_Lush.terrainlayer";
        private const string SoilLayerPath = "Assets/Art/TerrainLayers/TL_Soil_Dirt.terrainlayer";
        private const string RockLayerPath = "Assets/Art/TerrainLayers/TL_Rock_Cliff.terrainlayer";
        private const string LegacyLayerPath = "Assets/NewLayer.terrainlayer";
        private const string BuildReportPath = "Assets/Reports/CastleTerrain_Final.txt";

        private const float TerrainWidth = 180f;
        private const float TerrainHeight = 35f;
        private const float PlateauElevation = 12f;

        [MenuItem("Tools/Castle Terrain/2 - Configure Materials", priority = 101)]
        public static void ConfigureMaterials()
        {
            ConfigureColorTexture(GrassDiffuse);
            ConfigureNormalTexture(GrassNormal);
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Grass/leafy_grass_rough_2k.jpg");
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Grass/leafy_grass_ao_2k.jpg");
            ConfigureColorTexture(SoilDiffuse);
            ConfigureNormalTexture(SoilNormal);
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Soil/dirt_rough_2k.jpg");
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Soil/dirt_ao_2k.jpg");
            ConfigureColorTexture(RockDiffuse);
            ConfigureNormalTexture(RockNormal);
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Rock/rock_01_rough_2k.jpg");
            ConfigureDataTexture("Assets/Art/Textures/Terrain/Rock/rock_01_ao_2k.jpg");

            CreateOrUpdateLayer(GrassLayerPath, GrassDiffuse, GrassNormal, new Vector2(10f, 10f), 0.1f,
                new Vector4(0.72f, 1.08f, 0.64f, 1f));
            CreateOrUpdateLayer(SoilLayerPath, SoilDiffuse, SoilNormal, new Vector2(8f, 8f), 0.05f,
                new Vector4(0.72f, 0.68f, 0.56f, 1f));
            CreateOrUpdateLayer(RockLayerPath, RockDiffuse, RockNormal, new Vector2(6.5f, 6.5f), 0.08f,
                new Vector4(0.62f, 0.68f, 0.7f, 1f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CASTLE_TERRAIN_MATERIALS_OK: three URP Terrain Layers configured from CC0 2K textures.");
        }

        [MenuItem("Tools/Castle Terrain/3 - Build Natural Highland", priority = 102)]
        public static void BuildNaturalHighland()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            ConfigureMaterials();

            GameObject castle = GameObject.Find("castle-with-keep");
            Terrain terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (castle == null || terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Castle or Terrain is missing from Castle_Exterior.");
            }

            TerrainData data = terrain.terrainData;
            Bounds castleBounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            float platformRadiusX = castleBounds.extents.x + 12f;
            float platformRadiusZ = castleBounds.extents.z + 12f;
            float hillRadiusX = 78f;
            float hillRadiusZ = 74f;

            Undo.RegisterCompleteObjectUndo(data, "Generate castle highland");
            Undo.RecordObject(terrain.transform, "Position castle highland");
            if (data.heightmapResolution != 513)
            {
                data.heightmapResolution = 513;
            }

            data.size = new Vector3(TerrainWidth, TerrainHeight, TerrainWidth);
            data.alphamapResolution = 512;
            data.baseMapResolution = 1024;
            terrain.transform.position = new Vector3(
                castleBounds.center.x - TerrainWidth * 0.5f,
                castleBounds.min.y - PlateauElevation,
                castleBounds.center.z - TerrainWidth * 0.5f);

            float[,] heights = GenerateHeights(
                data.heightmapResolution,
                data.size,
                platformRadiusX,
                platformRadiusZ,
                hillRadiusX,
                hillRadiusZ);
            data.SetHeights(0, 0, heights);
            data.SyncHeightmap();

            TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
            TerrainLayer soil = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SoilLayerPath);
            TerrainLayer rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RockLayerPath);
            TerrainLayer legacy = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LegacyLayerPath);
            data.terrainLayers = legacy != null
                ? new[] { grass, soil, rock, legacy }
                : new[] { grass, soil, rock };

            float[,,] alphamaps = GenerateAlphamaps(heights, data.alphamapResolution, data.size, data.terrainLayers.Length);
            data.SetAlphamaps(0, 0, alphamaps);

            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            WriteBuildReport(terrain, castleBounds, platformRadiusX, platformRadiusZ, hillRadiusX, hillRadiusZ);
            Selection.activeGameObject = terrain.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("CASTLE_TERRAIN_BUILD_OK: natural highland generated and grass/soil/rock alphamaps applied.");
        }

        private static float[,] GenerateHeights(
            int resolution,
            Vector3 terrainSize,
            float platformRadiusX,
            float platformRadiusZ,
            float hillRadiusX,
            float hillRadiusZ)
        {
            var heights = new float[resolution, resolution];
            float normalizedPlateau = PlateauElevation / terrainSize.y;
            for (int z = 0; z < resolution; z++)
            {
                float offsetZ = (z / (resolution - 1f) - 0.5f) * terrainSize.z;
                for (int x = 0; x < resolution; x++)
                {
                    float offsetX = (x / (resolution - 1f) - 0.5f) * terrainSize.x;
                    float platformDistance =
                        offsetX * offsetX / (platformRadiusX * platformRadiusX) +
                        offsetZ * offsetZ / (platformRadiusZ * platformRadiusZ);
                    if (platformDistance <= 1f)
                    {
                        heights[z, x] = normalizedPlateau;
                        continue;
                    }

                    float broadNoise = Mathf.PerlinNoise((offsetX + 215f) / 58f, (offsetZ - 91f) / 58f);
                    float secondaryNoise = Mathf.PerlinNoise((offsetX - 47f) / 24f, (offsetZ + 133f) / 24f);
                    float warpedX = offsetX + (secondaryNoise - 0.5f) * 12f + offsetZ * 0.045f;
                    float warpedZ = offsetZ + (broadNoise - 0.5f) * 14f - offsetX * 0.035f;
                    float radiusVariation = 0.8f + broadNoise * 0.34f;
                    float falloff = CastleTerrainMath.PlateauFalloff(
                        warpedX,
                        warpedZ,
                        platformRadiusX,
                        platformRadiusZ,
                        hillRadiusX * radiusVariation,
                        hillRadiusZ * radiusVariation);
                    float slopeNoiseMeters = ((broadNoise - 0.5f) * 2.2f + (secondaryNoise - 0.5f) * 1.1f)
                        * falloff * (1f - falloff) * 2.6f;
                    float heightMeters = PlateauElevation * falloff + slopeNoiseMeters;
                    heights[z, x] = Mathf.Clamp01(heightMeters / terrainSize.y);
                }
            }

            return heights;
        }

        private static float[,,] GenerateAlphamaps(float[,] heights, int resolution, Vector3 terrainSize, int layerCount)
        {
            var maps = new float[resolution, resolution, layerCount];
            int heightResolution = heights.GetLength(0);
            float cellX = terrainSize.x / (heightResolution - 1f);
            float cellZ = terrainSize.z / (heightResolution - 1f);
            float normalizedPlateau = PlateauElevation / terrainSize.y;

            for (int z = 0; z < resolution; z++)
            {
                int hz = Mathf.Clamp(Mathf.RoundToInt(z / (resolution - 1f) * (heightResolution - 1f)), 1, heightResolution - 2);
                float worldZ = (z / (resolution - 1f) - 0.5f) * terrainSize.z;
                for (int x = 0; x < resolution; x++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x / (resolution - 1f) * (heightResolution - 1f)), 1, heightResolution - 2);
                    float worldX = (x / (resolution - 1f) - 0.5f) * terrainSize.x;
                    float derivativeX = (heights[hz, hx + 1] - heights[hz, hx - 1]) * terrainSize.y / (2f * cellX);
                    float derivativeZ = (heights[hz + 1, hx] - heights[hz - 1, hx]) * terrainSize.y / (2f * cellZ);
                    float slope = Mathf.Clamp01(Mathf.Sqrt(derivativeX * derivativeX + derivativeZ * derivativeZ));
                    float falloff = Mathf.Clamp01(heights[hz, hx] / normalizedPlateau);
                    float variation = Mathf.PerlinNoise((worldX + 311f) / 24f, (worldZ - 173f) / 24f);
                    Vector4 weights = CastleTerrainMath.CalculateSurfaceWeights(slope, falloff, variation);

                    maps[z, x, 0] = weights.x;
                    maps[z, x, 1] = weights.y;
                    maps[z, x, 2] = weights.z;
                    if (layerCount > 3)
                    {
                        maps[z, x, 3] = weights.w;
                    }
                }
            }

            return maps;
        }

        private static void ConfigureColorTexture(string path)
        {
            ConfigureTexture(path, TextureImporterType.Default, true, 4);
        }

        private static void ConfigureNormalTexture(string path)
        {
            ConfigureTexture(path, TextureImporterType.NormalMap, false, 4);
        }

        private static void ConfigureDataTexture(string path)
        {
            ConfigureTexture(path, TextureImporterType.Default, false, 1);
        }

        private static void ConfigureTexture(string path, TextureImporterType type, bool sRgb, int anisoLevel)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"Texture asset was not imported: {path}");
            }

            bool changed = importer.textureType != type || importer.sRGBTexture != sRgb ||
                importer.maxTextureSize != 2048 || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.anisoLevel != anisoLevel;
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = anisoLevel;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void CreateOrUpdateLayer(
            string path,
            string diffusePath,
            string normalPath,
            Vector2 tileSize,
            float smoothness,
            Vector4 diffuseRemapMax)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            layer.tileSize = tileSize;
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = smoothness;
            layer.normalScale = 1f;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = diffuseRemapMax;
            EditorUtility.SetDirty(layer);
        }

        private static void WriteBuildReport(
            Terrain terrain,
            Bounds castleBounds,
            float platformRadiusX,
            float platformRadiusZ,
            float hillRadiusX,
            float hillRadiusZ)
        {
            TerrainData data = terrain.terrainData;
            var report = new StringBuilder();
            report.AppendLine("Castle Terrain Final Configuration");
            report.AppendLine($"GeneratedUtc={DateTime.UtcNow:O}");
            report.AppendLine($"CastleBoundsCenter={Format(castleBounds.center)}");
            report.AppendLine($"CastleBoundsSize={Format(castleBounds.size)}");
            report.AppendLine($"TerrainPosition={Format(terrain.transform.position)}");
            report.AppendLine($"TerrainSize={Format(data.size)}");
            report.AppendLine($"HeightmapResolution={data.heightmapResolution}");
            report.AppendLine($"AlphamapResolution={data.alphamapResolution}");
            report.AppendLine($"BaseMapResolution={data.baseMapResolution}");
            report.AppendLine($"PlatformRadius=({platformRadiusX:0.###}, {platformRadiusZ:0.###})");
            report.AppendLine($"HillRadius=({hillRadiusX:0.###}, {hillRadiusZ:0.###})");
            report.AppendLine($"PlatformWorldHeight={terrain.transform.position.y + PlateauElevation:0.###}");
            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                TerrainLayer layer = data.terrainLayers[i];
                report.AppendLine($"Layer[{i}]={layer.name}; Tile={layer.tileSize.x:0.###}x{layer.tileSize.y:0.###}; Metallic={layer.metallic:0.###}; Smoothness={layer.smoothness:0.###}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BuildReportPath) ?? "Assets/Reports");
            File.WriteAllText(BuildReportPath, report.ToString());
            AssetDatabase.ImportAsset(BuildReportPath, ImportAssetOptions.ForceUpdate);
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
