using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedievalCastle.Editor
{
    public static class CastleEnvironmentBuilder
    {
        private const string RootName = "Environment_Dressing";
        private const string TreeRoot = "Assets/Moudles/Realistic Tree/Prefabs/URP/";
        private const string PropRoot = "Assets/Moudles/RPG MEDIEVAL PROPS/UNIVERSAL RENDER PIPELINE/Prefabs/";
        private const string ReportPath = "Assets/Reports/CastleEnvironment_Layout.txt";
        private const string MaterialDirectory = "Assets/Art/Materials/Environment";

        private readonly struct TreeSpec
        {
            public TreeSpec(string asset, float x, float z, float yaw, float height)
            {
                Asset = asset;
                Position = new Vector2(x, z);
                Yaw = yaw;
                Height = height;
            }

            public string Asset { get; }
            public Vector2 Position { get; }
            public float Yaw { get; }
            public float Height { get; }
        }

        private readonly struct PropSpec
        {
            public PropSpec(string asset, float x, float z, float yaw, float scale = 1f, float targetHeight = 0f)
            {
                Asset = asset;
                Position = new Vector2(x, z);
                Yaw = yaw;
                Scale = scale;
                TargetHeight = targetHeight;
            }

            public string Asset { get; }
            public Vector2 Position { get; }
            public float Yaw { get; }
            public float Scale { get; }
            public float TargetHeight { get; }
        }

        [MenuItem("Tools/Castle Environment/1 - Build First-Pass Dressing", priority = 200)]
        public static void BuildFirstPass()
        {
            CastleTerrainAudit.EnsureTargetSceneIsOpen();
            GameObject castle = GameObject.Find("castle-with-keep");
            Terrain terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (castle == null || terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Castle or Terrain is missing from Castle_Exterior.");
            }

            Bounds castleBounds = CastleTerrainAudit.CalculateRendererBounds(castle);
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot);
            }

            GameObject root = CreateRoot(RootName, null);
            Transform entranceRoad = CreateRoot("EntranceRoad", root.transform).transform;
            Transform guard = CreateRoot("GuardCheckpoint", root.transform).transform;
            Transform market = CreateRoot("MarketZone", root.transform).transform;
            Transform training = CreateRoot("TrainingYard", root.transform).transform;
            Transform forest = CreateRoot("ForestPerimeter", root.transform).transform;
            Transform ground = CreateRoot("GroundDetails", root.transform).transform;
            Material wood = GetOrCreateMaterial("MAT_Wood_Dark", new Color(0.19f, 0.12f, 0.07f));
            Material cloth = GetOrCreateMaterial("MAT_Cloth_Burgundy", new Color(0.32f, 0.07f, 0.065f));
            Material straw = GetOrCreateMaterial("MAT_Straw", new Color(0.52f, 0.37f, 0.14f));

            Vector3 center = castleBounds.center;
            int treeCount = PlaceTrees(terrain, castleBounds, center, forest);
            int guardCount = PlaceGuardProps(terrain, castleBounds, guard);
            int marketCount = PlaceMarketProps(terrain, castleBounds, market);
            int trainingCount = PlaceTrainingProps(terrain, castleBounds, training);
            int groundCount = PlaceGroundDetails(terrain, center, ground);
            CreateMarketStall(terrain, CastleEnvironmentLayout.MarketCenter(castleBounds), market, wood, cloth);
            CreateTrainingDummy(terrain, CastleEnvironmentLayout.TrainingCenter(castleBounds) + new Vector3(-2.2f, 0f, 1.5f), training, wood, straw, "TrainingDummy_A");
            CreateTrainingDummy(terrain, CastleEnvironmentLayout.TrainingCenter(castleBounds) + new Vector3(2.2f, 0f, 1.5f), training, wood, straw, "TrainingDummy_B");

            entranceRoad.position = GroundPoint(terrain, CastleEnvironmentLayout.GateAnchor(castleBounds));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            WriteReport(castleBounds, treeCount, guardCount, marketCount, trainingCount, groundCount);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"CASTLE_ENVIRONMENT_BUILD_OK: trees={treeCount}; guard={guardCount}; market={marketCount}; training={trainingCount}; ground={groundCount}");
        }

        private static int PlaceTrees(Terrain terrain, Bounds castleBounds, Vector3 center, Transform parent)
        {
            TreeSpec[] specs =
            {
                new TreeSpec("Spruce/Spruce 3.prefab", -66f, -52f, 18f, 13f),
                new TreeSpec("Spruce/Spruce 6.prefab", -45f, -68f, 95f, 14f),
                new TreeSpec("Spruce/Spruce 8.prefab", -18f, -73f, 210f, 12f),
                new TreeSpec("Spruce/Spruce 3.prefab", 18f, -72f, 145f, 13.5f),
                new TreeSpec("Spruce/Spruce 6.prefab", 45f, -67f, 260f, 14.5f),
                new TreeSpec("Spruce/Spruce 8.prefab", 67f, -51f, 320f, 12.5f),
                new TreeSpec("Ash/Ash 3.prefab", -72f, -24f, 35f, 11f),
                new TreeSpec("Chestnut/Chestnut 5.prefab", -73f, 7f, 155f, 10.5f),
                new TreeSpec("Ash/Ash 5.prefab", -67f, 37f, 275f, 12f),
                new TreeSpec("Chestnut/Chestnut 2.prefab", -55f, 61f, 15f, 10f),
                new TreeSpec("Birch/Birch 6.prefab", -38f, 73f, 130f, 9.5f),
                new TreeSpec("Birch/Birch 2.prefab", 38f, 73f, 240f, 9f),
                new TreeSpec("Chestnut/Chestnut 5.prefab", 55f, 61f, 75f, 10.5f),
                new TreeSpec("Ash/Ash 3.prefab", 67f, 38f, 190f, 11.5f),
                new TreeSpec("Chestnut/Chestnut 2.prefab", 73f, 8f, 300f, 10f),
                new TreeSpec("Ash/Ash 5.prefab", 71f, -25f, 105f, 12f),
                new TreeSpec("Birch/Birch 9.prefab", -34f, 56f, 40f, 8.5f),
                new TreeSpec("Ash/Ash 3.prefab", -24f, 64f, 170f, 10.5f),
                new TreeSpec("Birch/Birch 6.prefab", 24f, 64f, 280f, 9f),
                new TreeSpec("Chestnut/Chestnut 5.prefab", 34f, 55f, 355f, 10f),
                new TreeSpec("Birch/Birch 2.prefab", -57f, 45f, 220f, 8f),
                new TreeSpec("Birch/Birch 9.prefab", 58f, 46f, 125f, 8.5f),
                new TreeSpec("Spruce/Spruce 8.prefab", -58f, -41f, 65f, 11.5f),
                new TreeSpec("Spruce/Spruce 3.prefab", 58f, -40f, 245f, 12f),
            };

            int count = 0;
            foreach (TreeSpec spec in specs)
            {
                Vector3 position = center + new Vector3(spec.Position.x, 0f, spec.Position.y);
                if (CastleEnvironmentLayout.IsInsideEntranceCorridor(position, castleBounds))
                {
                    continue;
                }

                GameObject tree = PlacePrefab(TreeRoot + spec.Asset, terrain, position, spec.Yaw, 1f, spec.Height, parent);
                tree.name = Path.GetFileNameWithoutExtension(spec.Asset);
                foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }

                count++;
            }

            return count;
        }

        private static int PlaceGuardProps(Terrain terrain, Bounds castleBounds, Transform parent)
        {
            Vector3 zone = CastleEnvironmentLayout.GuardCenter(castleBounds);
            PropSpec[] specs =
            {
                new PropSpec("Fence1.prefab", -6.5f, 0f, 90f),
                new PropSpec("Fence2.prefab", 6.5f, 0f, 90f),
                new PropSpec("FencePost.prefab", -4.2f, 0.5f, 0f),
                new PropSpec("FencePost.prefab", 4.2f, 0.5f, 0f),
                new PropSpec("Crate1.prefab", 7.2f, -1.5f, 18f),
                new PropSpec("Crate2.prefab", 8.1f, -0.5f, 338f),
                new PropSpec("Barel1.prefab", -7.4f, -1.3f, 12f),
                new PropSpec("Bucket.prefab", -8.1f, 0.1f, 300f),
            };
            return PlaceZoneProps(specs, zone, terrain, parent);
        }

        private static int PlaceMarketProps(Terrain terrain, Bounds castleBounds, Transform parent)
        {
            Vector3 zone = CastleEnvironmentLayout.MarketCenter(castleBounds);
            PropSpec[] specs =
            {
                new PropSpec("Crate1.prefab", -3f, -2f, 8f),
                new PropSpec("Crate2.prefab", -1.8f, -2.6f, 350f),
                new PropSpec("Crate2Open.prefab", -0.6f, -2.2f, 18f),
                new PropSpec("Crate1.prefab", 2.8f, 1.8f, 32f),
                new PropSpec("Barel1.prefab", 3.3f, -0.8f, 0f),
                new PropSpec("Barel2Open.prefab", 4.2f, 0.6f, 25f),
                new PropSpec("BucketLarge.prefab", 1.2f, 2.7f, 310f),
                new PropSpec("Bucket.prefab", -2.6f, 2.8f, 40f),
                new PropSpec("Rope.prefab", 0.7f, -3.1f, 110f),
                new PropSpec("PlanksPile.prefab", 4.8f, 2.8f, 80f),
            };
            return PlaceZoneProps(specs, zone, terrain, parent);
        }

        private static int PlaceTrainingProps(Terrain terrain, Bounds castleBounds, Transform parent)
        {
            Vector3 zone = CastleEnvironmentLayout.TrainingCenter(castleBounds);
            PropSpec[] specs =
            {
                new PropSpec("Fence1.prefab", -7f, -7f, 0f),
                new PropSpec("Fence2.prefab", -2.5f, -7f, 0f),
                new PropSpec("Fence1.prefab", 2.5f, -7f, 0f),
                new PropSpec("Fence2.prefab", 7f, -7f, 0f),
                new PropSpec("Fence1.prefab", -7f, 7f, 0f),
                new PropSpec("Fence2.prefab", -2.5f, 7f, 0f),
                new PropSpec("Fence1.prefab", 2.5f, 7f, 0f),
                new PropSpec("Fence2.prefab", 7f, 7f, 0f),
                new PropSpec("Trunk.prefab", -5f, 2.4f, 65f),
                new PropSpec("PlanksPile.prefab", 5.3f, 2.7f, 100f),
                new PropSpec("Axe1.prefab", -5.7f, 1.8f, 30f),
                new PropSpec("Hammer.prefab", 4.8f, 1.8f, 125f),
                new PropSpec("Pickaxe.prefab", 5.8f, 1.4f, 210f),
                new PropSpec("Crate1.prefab", -5.8f, -2.5f, 20f),
                new PropSpec("Barel1.prefab", 5.6f, -2.6f, 0f),
            };
            return PlaceZoneProps(specs, zone, terrain, parent);
        }

        private static int PlaceGroundDetails(Terrain terrain, Vector3 center, Transform parent)
        {
            PropSpec[] specs =
            {
                new PropSpec("RockBig.prefab", -48f, 50f, 35f, 1f, 2.4f),
                new PropSpec("RockMedium.prefab", -42f, 58f, 140f, 1f, 1.5f),
                new PropSpec("Rock.prefab", -31f, 69f, 260f, 1f, 0.9f),
                new PropSpec("RockMedium.prefab", 31f, 68f, 70f, 1f, 1.4f),
                new PropSpec("RockBig.prefab", 47f, 52f, 205f, 1f, 2.2f),
                new PropSpec("Rock.prefab", 61f, 31f, 315f, 1f, 0.8f),
                new PropSpec("RockMedium.prefab", -62f, 28f, 110f, 1f, 1.2f),
                new PropSpec("Rock.prefab", -65f, -11f, 20f, 1f, 0.9f),
                new PropSpec("RockBig.prefab", 65f, -12f, 170f, 1f, 2.0f),
                new PropSpec("Trunk.prefab", -46f, 39f, 55f),
                new PropSpec("Trunk.prefab", 52f, 37f, 120f),
                new PropSpec("LongGrass.prefab", -35f, 49f, 15f, 1f, 0.8f),
                new PropSpec("LongGrass.prefab", -39f, 53f, 95f, 1f, 0.7f),
                new PropSpec("LongGrass.prefab", 36f, 50f, 180f, 1f, 0.8f),
                new PropSpec("LongGrass.prefab", 41f, 55f, 270f, 1f, 0.7f),
                new PropSpec("Dandelion.prefab", -29f, 60f, 0f, 1f, 0.45f),
                new PropSpec("Dandelion.prefab", 29f, 59f, 60f, 1f, 0.45f),
            };

            int count = 0;
            foreach (PropSpec spec in specs)
            {
                Vector3 position = center + new Vector3(spec.Position.x, 0f, spec.Position.y);
                PlacePrefab(PropRoot + spec.Asset, terrain, position, spec.Yaw, spec.Scale, spec.TargetHeight, parent);
                count++;
            }
            return count;
        }

        private static int PlaceZoneProps(IEnumerable<PropSpec> specs, Vector3 zone, Terrain terrain, Transform parent)
        {
            int count = 0;
            foreach (PropSpec spec in specs)
            {
                Vector3 position = zone + new Vector3(spec.Position.x, 0f, spec.Position.y);
                PlacePrefab(PropRoot + spec.Asset, terrain, position, spec.Yaw, spec.Scale, spec.TargetHeight, parent);
                count++;
            }
            return count;
        }

        private static GameObject PlacePrefab(
            string path,
            Terrain terrain,
            Vector3 position,
            float yaw,
            float scale,
            float targetHeight,
            Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Environment prefab was not found: {path}");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Place castle environment prefab");
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale *= scale;
            if (targetHeight > 0f)
            {
                Bounds initialBounds = CalculateBounds(instance);
                if (initialBounds.size.y > 0.001f)
                {
                    instance.transform.localScale *= targetHeight / initialBounds.size.y;
                }
            }

            Vector3 ground = GroundPoint(terrain, position);
            instance.transform.position = ground;
            Bounds bounds = CalculateBounds(instance);
            instance.transform.position += Vector3.up * (ground.y - bounds.min.y);
            GameObjectUtility.SetStaticEditorFlags(instance,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return instance;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"Placed object has no Renderer: {root.name}");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static Vector3 GroundPoint(Terrain terrain, Vector3 position)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create castle environment root");
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void CreateMarketStall(
            Terrain terrain,
            Vector3 position,
            Transform parent,
            Material wood,
            Material cloth)
        {
            GameObject root = CreateRoot("MarketStall", parent);
            root.transform.position = GroundPoint(terrain, position + new Vector3(0f, 0f, 1.2f));
            CreatePart(root.transform, "Post_FL", PrimitiveType.Cube, new Vector3(-2f, 1.25f, -1.25f), new Vector3(0.18f, 2.5f, 0.18f), wood);
            CreatePart(root.transform, "Post_FR", PrimitiveType.Cube, new Vector3(2f, 1.25f, -1.25f), new Vector3(0.18f, 2.5f, 0.18f), wood);
            CreatePart(root.transform, "Post_BL", PrimitiveType.Cube, new Vector3(-2f, 1.25f, 1.25f), new Vector3(0.18f, 2.5f, 0.18f), wood);
            CreatePart(root.transform, "Post_BR", PrimitiveType.Cube, new Vector3(2f, 1.25f, 1.25f), new Vector3(0.18f, 2.5f, 0.18f), wood);
            CreatePart(root.transform, "Counter", PrimitiveType.Cube, new Vector3(0f, 0.9f, -1.05f), new Vector3(4.2f, 0.18f, 0.8f), wood);
            CreatePart(root.transform, "Canopy", PrimitiveType.Cube, new Vector3(0f, 2.62f, 0f), new Vector3(4.8f, 0.12f, 3.3f), cloth);
            CreatePart(root.transform, "BackShelf", PrimitiveType.Cube, new Vector3(0f, 1.25f, 1.1f), new Vector3(3.6f, 0.14f, 0.55f), wood);
        }

        private static void CreateTrainingDummy(
            Terrain terrain,
            Vector3 position,
            Transform parent,
            Material wood,
            Material straw,
            string name)
        {
            GameObject root = CreateRoot(name, parent);
            root.transform.position = GroundPoint(terrain, position);
            CreatePart(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(1.4f, 0.16f, 0.65f), wood);
            CreatePart(root.transform, "Post", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.2f, 2.2f, 0.2f), wood);
            CreatePart(root.transform, "Crossbar", PrimitiveType.Cube, new Vector3(0f, 1.75f, 0f), new Vector3(1.65f, 0.16f, 0.16f), wood);
            CreatePart(root.transform, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.5f, 0f), new Vector3(0.65f, 0.55f, 0.42f), straw);
            CreatePart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.25f, 0f), new Vector3(0.45f, 0.45f, 0.45f), straw);
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            Undo.RegisterCreatedObjectUndo(part, "Create castle environment structure");
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(part,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return part;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialDirectory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Materials");
                }
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Environment");
            }

            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteReport(
            Bounds castleBounds,
            int trees,
            int guard,
            int market,
            int training,
            int ground)
        {
            var report = new StringBuilder();
            report.AppendLine("Castle Environment First-Pass Layout");
            report.AppendLine($"GeneratedUtc={DateTime.UtcNow:O}");
            report.AppendLine($"Scene={SceneManager.GetActiveScene().path}");
            report.AppendLine($"GateAnchor={CastleEnvironmentLayout.GateAnchor(castleBounds)}");
            report.AppendLine($"GuardCenter={CastleEnvironmentLayout.GuardCenter(castleBounds)}");
            report.AppendLine($"MarketCenter={CastleEnvironmentLayout.MarketCenter(castleBounds)}");
            report.AppendLine($"TrainingCenter={CastleEnvironmentLayout.TrainingCenter(castleBounds)}");
            report.AppendLine($"Trees={trees}");
            report.AppendLine($"GuardProps={guard}");
            report.AppendLine($"MarketProps={market}");
            report.AppendLine($"TrainingProps={training}");
            report.AppendLine($"GroundDetails={ground}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Reports");
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
