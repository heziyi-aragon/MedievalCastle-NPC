using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MedievalCastle.Editor
{
    public static class CastleEnvironmentCapture
    {
        private const string OutputDirectory = "Assets/Reports/Screenshots";

        [MenuItem("Tools/Castle Environment/2 - Capture Entrance Review", priority = 201)]
        public static void CaptureEntranceReview()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            Camera source = Camera.main;
            if (castle == null || source == null || GameObject.Find("Environment_Dressing") == null)
            {
                throw new InvalidOperationException("Castle, Main Camera, or Environment_Dressing is missing.");
            }

            Directory.CreateDirectory(OutputDirectory);
            Bounds bounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            Vector3 gate = CastleEnvironmentLayout.GateAnchor(bounds);
            GameObject cameraObject = new GameObject("Environment Review Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.fieldOfView = 48f;
                Capture(camera, gate + new Vector3(0f, 30f, 62f), gate + new Vector3(0f, 0f, 12f),
                    OutputDirectory + "/Castle_Environment_Front.png");
                Capture(camera, gate + new Vector3(55f, 42f, 62f), gate + new Vector3(0f, 0f, 10f),
                    OutputDirectory + "/Castle_Environment_Oblique.png");
                Capture(camera, gate + new Vector3(0f, 82f, 24f), gate + new Vector3(0f, 0f, 10f),
                    OutputDirectory + "/Castle_Environment_Top.png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("CASTLE_ENVIRONMENT_CAPTURE_OK: front, oblique, and top review images written.");
        }

        private static void Capture(Camera camera, Vector3 position, Vector3 target, string path)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
