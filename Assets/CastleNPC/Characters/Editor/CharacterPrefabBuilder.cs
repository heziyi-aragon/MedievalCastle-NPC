using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MedievalCastle.NPC.Editor
{
    public static class CharacterPrefabBuilder
    {
        private const string Folder = "Assets/CastleNPC/Characters";

        [MenuItem("Tools/Castle NPC/Build Character Prefabs")]
        public static void Build()
        {
            foreach (var name in new[] { "Guard", "Merchant", "Knight" })
                BuildCharacter(name);
            AssetDatabase.SaveAssets();
        }

        private static void BuildCharacter(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Models/{name}.glb");
            if (model == null)
                throw new InvalidOperationException($"The {name} model has not been imported.");

            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = name + " Model";
                var settings = new SerializedObject(root.AddComponent<CastleNpcCharacter>());
                settings.FindProperty("roleId").stringValue = name.ToLowerInvariant();
                var bones = visual.GetComponentsInChildren<Transform>(true);
                Transform Bone(string id) => bones.Single(t => t.name == id);
                foreach (var entry in new[] { ("chest", "Chest"), ("head", "Head"), ("jaw", "Jaw"), ("gestureArm", "GestureArm") })
                    settings.FindProperty(entry.Item1).objectReferenceValue = Bone(entry.Item2);
                var body = root.AddComponent<CapsuleCollider>();
                var headPosition = Bone("Head").position;
                if (name == "Knight")
                {
                    foreach (var entry in new[] { ("horseChest", "HorseChest"), ("earL", "EarL"), ("earR", "EarR") })
                        settings.FindProperty(entry.Item1).objectReferenceValue = Bone(entry.Item2);
                    body.radius = .25f;
                    body.center = headPosition - Vector3.up * .28f;
                    body.height = .98f;
                    var rump = Bone("HorseRump").position;
                    var withers = Bone("HorseWithers").position;
                    var muzzle = Bone("HorseMuzzle").position;
                    var poll = (Bone("EarL").position + Bone("EarR").position) * .5f - Vector3.up * .13f;
                    AddCapsule(root.transform, "Horse barrel", rump, withers, .31f);
                    AddCapsule(root.transform, "Horse neck", withers, poll, .20f);
                    AddCapsule(root.transform, "Horse head", poll, muzzle, .14f);
                }
                else
                {
                    body.radius = .32f;
                    body.height = visual.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y);
                    body.center = Vector3.up * (body.height / 2f);
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                var path = $"{Folder}/Prefabs/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved)
                    throw new InvalidOperationException($"Unity could not save {path}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddCapsule(Transform parent, string name, Vector3 start, Vector3 end, float radius)
        {
            var shape = new GameObject(name).transform;
            shape.SetParent(parent, false);
            shape.SetPositionAndRotation((start + end) * .5f, Quaternion.FromToRotation(Vector3.up, end - start));
            var collider = shape.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = Vector3.Distance(start, end) + 2f * radius;
        }
    }
}
