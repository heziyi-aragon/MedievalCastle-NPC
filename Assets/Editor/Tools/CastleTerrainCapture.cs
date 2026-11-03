using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedievalCastle.Editor
{
    public static class CastleTerrainCapture
    {
        private const string OutputDirectory = "Assets/Reports/Screenshots";
        private const string GamePath = OutputDirectory + "/Castle_Exterior_Game_Final.png";
        private const string ScenePath = OutputDirectory + "/Castle_Exterior_Scene_Final.png";
        private const string SceneNorthPath = OutputDirectory + "/Castle_Exterior_Scene_North.png";
        private const string SceneEastPath = OutputDirectory + "/Castle_Exterior_Scene_East.png";
        private const string SceneSouthPath = OutputDirectory + "/Castle_Exterior_Scene_South.png";
        private const string SceneWestPath = OutputDirectory + "/Castle_Exterior_Scene_West.png";
        private const string ValidationPath = "Assets/Reports/CastleTerrain_Validation.txt";

        [MenuItem("Tools/Castle Terrain/4 - Capture Final Evidence", priority = 103)]
        public static void CaptureFinalEvidence()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            Terrain terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            Camera gameCamera = Camera.main;
            if (castle == null || terrain == null || terrain.terrainData == null || gameCamera == null)
            {
                throw new InvalidOperationException("Castle, Terrain, TerrainData, or Main Camera is missing.");
            }

            Directory.CreateDirectory(OutputDirectory);
            CaptureCamera(gameCamera, GamePath);

            Bounds bounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            GameObject overviewObject = new GameObject("Castle Terrain Evidence Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                Camera overviewCamera = overviewObject.AddComponent<Camera>();
                overviewCamera.CopyFrom(gameCamera);
                overviewCamera.fieldOfView = 48f;
                CaptureOverview(overviewCamera, bounds, new Vector3(58f, 44f, 68f), ScenePath);
                CaptureOverview(overviewCamera, bounds, new Vector3(0f, 44f, 72f), SceneNorthPath);
                CaptureOverview(overviewCamera, bounds, new Vector3(72f, 44f, 0f), SceneEastPath);
                CaptureOverview(overviewCamera, bounds, new Vector3(0f, 44f, -72f), SceneSouthPath);
                CaptureOverview(overviewCamera, bounds, new Vector3(-72f, 44f, 0f), SceneWestPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overviewObject);
            }

            WriteValidationReport(castle, terrain, bounds);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"CASTLE_TERRAIN_EVIDENCE_OK: {GamePath}; {ScenePath}; {ValidationPath}");
        }

        private static void CaptureOverview(Camera camera, Bounds bounds, Vector3 offset, string path)
        {
            camera.transform.position = bounds.center + offset;
            camera.transform.LookAt(bounds.center + Vector3.up * 1.5f);
            CaptureCamera(camera, path);
        }

        private static void CaptureCamera(Camera camera, string path)
        {
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void WriteValidationReport(GameObject castle, Terrain terrain, Bounds bounds)
        {
            TerrainData data = terrain.terrainData;
            (int errors, int warnings, int logs) = CastleTerrainAudit.GetConsoleCounts();
            float centerHeight = terrain.SampleHeight(bounds.center) + terrain.transform.position.y;
            var report = new StringBuilder();
            report.AppendLine("Castle Terrain Final Validation");
            report.AppendLine($"CapturedUtc={DateTime.UtcNow:O}");
            report.AppendLine($"Scene={SceneManager.GetActiveScene().path}");
            report.AppendLine($"CastlePosition={castle.transform.position}");
            report.AppendLine($"CastleRotation={castle.transform.eulerAngles}");
            report.AppendLine($"CastleScale={castle.transform.lossyScale}");
            report.AppendLine($"CastleBoundsMinY={bounds.min.y:0.###}");
            report.AppendLine($"TerrainCenterWorldY={centerHeight:0.###}");
            report.AppendLine($"TerrainSize={data.size}");
            report.AppendLine($"HeightmapResolution={data.heightmapResolution}");
            report.AppendLine($"AlphamapResolution={data.alphamapResolution}");
            report.AppendLine($"TerrainLayerCount={data.terrainLayers.Length}");
            report.AppendLine($"ConsoleErrors={errors}");
            report.AppendLine($"ConsoleWarnings={warnings}");
            report.AppendLine($"ConsoleLogs={logs}");
            report.AppendLine($"GameScreenshot={GamePath}");
            report.AppendLine($"SceneScreenshot={ScenePath}");
            report.AppendLine($"FourDirectionScreenshots={SceneNorthPath}; {SceneEastPath}; {SceneSouthPath}; {SceneWestPath}");
            File.WriteAllText(ValidationPath, report.ToString());
        }
    }
}
