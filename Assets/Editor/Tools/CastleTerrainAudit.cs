using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MedievalCastle.Editor
{
    public static class CastleTerrainAudit
    {
        private const string ScenePath = "Assets/Scenes/Castle_Exterior.unity";
        private const string ReportPath = "Assets/Reports/CastleTerrain_Baseline.txt";

        [MenuItem("Tools/Castle Terrain/1 - Capture Baseline", priority = 100)]
        public static void CaptureBaseline()
        {
            EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            Terrain terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (castle == null || terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Castle or Terrain is missing from Castle_Exterior.");
            }

            Bounds bounds = CalculateRendererBounds(castle);
            TerrainData data = terrain.terrainData;
            (int errors, int warnings, int logs) = GetConsoleCounts();
            Camera camera = Camera.main;

            var report = new StringBuilder();
            report.AppendLine("Castle Terrain Baseline");
            report.AppendLine($"CapturedUtc={DateTime.UtcNow:O}");
            report.AppendLine($"Scene={SceneManager.GetActiveScene().path}");
            report.AppendLine($"SceneDirty={SceneManager.GetActiveScene().isDirty}");
            report.AppendLine($"Unity={Application.unityVersion}");
            report.AppendLine($"Quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            report.AppendLine($"RenderPipeline={(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in")}");
            report.AppendLine($"CastlePosition={Format(castle.transform.position)}");
            report.AppendLine($"CastleRotation={Format(castle.transform.eulerAngles)}");
            report.AppendLine($"CastleScale={Format(castle.transform.lossyScale)}");
            report.AppendLine($"CastleBoundsCenter={Format(bounds.center)}");
            report.AppendLine($"CastleBoundsSize={Format(bounds.size)}");
            report.AppendLine($"CastleBoundsMin={Format(bounds.min)}");
            report.AppendLine($"CastleBoundsMax={Format(bounds.max)}");
            report.AppendLine($"TerrainPosition={Format(terrain.transform.position)}");
            report.AppendLine($"TerrainSize={Format(data.size)}");
            report.AppendLine($"HeightmapResolution={data.heightmapResolution}");
            report.AppendLine($"AlphamapResolution={data.alphamapResolution}");
            report.AppendLine($"BaseMapResolution={data.baseMapResolution}");
            report.AppendLine($"DetailResolution={data.detailResolution}");
            report.AppendLine($"TerrainLayerCount={data.terrainLayers.Length}");
            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                TerrainLayer layer = data.terrainLayers[i];
                report.AppendLine(layer == null
                    ? $"Layer[{i}]=<null>"
                    : $"Layer[{i}]={layer.name}; Tile={layer.tileSize.x:0.###}x{layer.tileSize.y:0.###}; Metallic={layer.metallic:0.###}; Smoothness={layer.smoothness:0.###}");
            }

            report.AppendLine($"MainCameraPosition={(camera != null ? Format(camera.transform.position) : "<missing>")}");
            report.AppendLine($"MainCameraFov={(camera != null ? camera.fieldOfView.ToString("0.###") : "<missing>")}");
            report.AppendLine($"ConsoleErrors={errors}");
            report.AppendLine($"ConsoleWarnings={warnings}");
            report.AppendLine($"ConsoleLogs={logs}");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Reports");
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"CASTLE_TERRAIN_BASELINE_OK\n{report}");
        }

        public static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled)
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("The castle has no enabled Renderer components.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        public static void EnsureTargetSceneIsOpen()
        {
            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                return;
            }

            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Scene switch canceled because the active scene has unsaved changes.");
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static (int errors, int warnings, int logs) GetConsoleCounts()
        {
            Type logEntriesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo method = logEntriesType?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return (-1, -1, -1);
            }

            object[] arguments = { 0, 0, 0 };
            method.Invoke(null, arguments);
            return ((int)arguments[0], (int)arguments[1], (int)arguments[2]);
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
