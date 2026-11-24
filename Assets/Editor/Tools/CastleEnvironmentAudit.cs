using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedievalCastle.Editor
{
    public static class CastleEnvironmentAudit
    {
        private const string ReportPath = "Assets/Reports/CastleEnvironment_Validation.txt";
        private static readonly string[] RequiredZones =
        {
            "EntranceRoad",
            "GuardCheckpoint",
            "MarketZone",
            "TrainingYard",
            "ForestPerimeter",
            "GroundDetails",
        };

        [MenuItem("Tools/Castle Environment/4 - Validate Environment", priority = 203)]
        public static void ValidateEnvironment()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            GameObject root = GameObject.Find("Environment_Dressing");
            if (castle == null || root == null)
            {
                throw new InvalidOperationException("Castle or Environment_Dressing is missing from Castle_Exterior.");
            }

            Bounds castleBounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            List<string> missingZones = RequiredZones
                .Where(zone => root.transform.Find(zone) == null)
                .ToList();
            Transform forest = root.transform.Find("ForestPerimeter");
            int treeCount = forest != null ? forest.childCount : 0;
            int enabledForestColliders = forest != null
                ? forest.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled)
                : 0;
            int corridorObstacles = CountCorridorObstacles(root.transform, castleBounds);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int missingMaterials = 0;
            int errorShaders = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        missingMaterials++;
                    }
                    else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    {
                        errorShaders++;
                    }
                }
            }

            (int errors, int warnings, int logs) = CastleTerrainAudit.GetConsoleCounts();
            bool passed = missingZones.Count == 0
                && treeCount >= 24
                && enabledForestColliders == 0
                && corridorObstacles == 0
                && missingMaterials == 0
                && errorShaders == 0
                && errors == 0;

            var report = new StringBuilder();
            report.AppendLine("Castle Environment Validation");
            report.AppendLine($"CapturedUtc={DateTime.UtcNow:O}");
            report.AppendLine($"Passed={passed}");
            report.AppendLine($"RequiredZones={RequiredZones.Length}");
            report.AppendLine($"MissingZones={(missingZones.Count == 0 ? "<none>" : string.Join(",", missingZones))}");
            report.AppendLine($"Trees={treeCount}");
            report.AppendLine($"EnabledForestColliders={enabledForestColliders}");
            report.AppendLine($"EntranceCorridorObstacles={corridorObstacles}");
            report.AppendLine($"EnvironmentRenderers={renderers.Length}");
            report.AppendLine($"MissingMaterialSlots={missingMaterials}");
            report.AppendLine($"ErrorShaderMaterials={errorShaders}");
            report.AppendLine($"ConsoleErrors={errors}");
            report.AppendLine($"ConsoleWarnings={warnings}");
            report.AppendLine($"ConsoleLogs={logs}");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Reports");
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);

            if (!passed)
            {
                throw new InvalidOperationException($"Environment validation failed. See {ReportPath}.\n{report}");
            }

            Debug.Log($"CASTLE_ENVIRONMENT_VALIDATION_OK\n{report}");
        }

        private static int CountCorridorObstacles(Transform root, Bounds castleBounds)
        {
            int count = 0;
            foreach (Transform zone in root)
            {
                foreach (Transform item in zone)
                {
                    if (item.GetComponentInChildren<Renderer>(true) != null
                        && CastleEnvironmentLayout.IsInsideEntranceCorridor(item.position, castleBounds))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
