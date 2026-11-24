using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MedievalCastle.Tests
{
    public sealed class CastlePlayerMotorTests
    {
        private Scene castleScene;

        [UnityTearDown]
        public IEnumerator UnloadCastle()
        {
            if (castleScene.IsValid() && castleScene.isLoaded) yield return SceneManager.UnloadSceneAsync(castleScene);
        }

        [Test]
        public void WalkingFollowsHorizontalHeadDirectionAndStopsAtSolids()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var root = new GameObject("Sampled walking player");
            var priorLock = CastlePlayerMotor.MovementLocked;
            try
            {
                // Keep this physics scenario separate from any open scene geometry.
                Vector3 start = new Vector3(1400, 20, 1400);
                floor.transform.position = start - Vector3.up * .5f;
                floor.transform.localScale = new Vector3(10, 1, 10);
                wall.transform.position = start + new Vector3(2, 1, 0);
                wall.transform.localScale = new Vector3(.15f, 2, 4);
                root.transform.position = start;
                var eye = new GameObject("Tracked camera").AddComponent<Camera>();
                eye.enabled = false;
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(.12f, 1.63f, .07f);
                eye.transform.localRotation = Quaternion.Euler(-47f, 90f, 0f);
                var motor = root.AddComponent<CastlePlayerMotor>();
                motor.Configure(eye, true);
                motor.enabled = false;
                CastlePlayerMotor.MovementLocked = false;
                Physics.SyncTransforms();
                for (var i = 0; i < 60; i++) motor.Step(Vector2.up, 0f, 1f / 60);
                Assert.That(root.transform.position.x - start.x, Is.InRange(1.15f, 1.6f), "1.6 m/s after acceleration");
                Assert.That(root.transform.position.y - start.y, Is.InRange(-.08f, .08f), "Looking up cannot lift the player");
                Assert.That(root.transform.position.z - start.z, Is.InRange(-.03f, .03f));
                Vector3 lockedEye = eye.transform.position;
                CastlePlayerMotor.MovementLocked = true;
                for (var i = 0; i < 20; i++) motor.Step(Vector2.up, 1f, 1f / 60);
                Assert.That(Vector3.Distance(eye.transform.position, lockedEye), Is.LessThan(.03f));
                Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.identity), Is.LessThan(.01f));
                CastlePlayerMotor.MovementLocked = false;
                for (var i = 0; i < 100; i++) motor.Step(Vector2.up, 0f, 1f / 60);
                var blocked = eye.transform.position;
                Assert.That(blocked.x - start.x, Is.InRange(1.55f, 1.85f), "Capsule stops before the wall");
                motor.Step(Vector2.zero, 1f, 1f / 60);
                for (var i = 0; i < 20; i++) motor.Step(Vector2.zero, 1f, 1f / 60);
                Assert.That(Mathf.DeltaAngle(root.transform.eulerAngles.y, 30f), Is.EqualTo(0f).Within(.05f));
                Assert.That(Vector3.Distance(eye.transform.position, blocked), Is.LessThan(.1f), "Snap about the physical eye");
                motor.Step(Vector2.zero, 0f, 1f / 60);
                motor.Step(Vector2.zero, -1f, 1f / 60);
                Assert.That(Mathf.DeltaAngle(root.transform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(.05f));
            }
            finally
            {
                CastlePlayerMotor.MovementLocked = priorLock;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(floor);
            }
        }

        [UnityTest]
        public IEnumerator CastleMasonryBlocksBothTowersAndLeavesTheCentralPassageOpen()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Castle_Exterior.unity", LoadSceneMode.Additive);
            castleScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var castle = castleScene.GetRootGameObjects().Single(o => o.name == "castle-with-keep");
            var shapes = castle.transform.Find("Castle masonry collision").GetComponents<Collider>();
            var grate = castle.GetComponentsInChildren<Transform>().Single(t => t.name == "Portcullis").GetComponent<BoxCollider>();
            Assert.That(grate, Is.Not.Null, "The moving grate carries its own collision");
            Physics.SyncTransforms();
            foreach (var offset in new[] { -4.4f, 0f, 4.4f })
            {
                var ray = new Ray(castle.transform.TransformPoint(offset, 1f, 29f), -castle.transform.forward);
                bool blocked = shapes.Any(shape => shape.Raycast(ray, out _, 15f));
                Assert.That(blocked, Is.EqualTo(offset != 0f), $"Approach x={offset}");
            }
            Assert.That(grate.Raycast(new Ray(new Vector3(0f, 1.75f, 28f), Vector3.back), out _, 10f), Is.False,
                "A standing player can pass beneath the raised grille");
            Assert.That(grate.Raycast(new Ray(new Vector3(0f, 2.25f, 28f), Vector3.back), out _, 10f), Is.True);
            grate.transform.Translate(Vector3.down, Space.World);
            Physics.SyncTransforms();
            Assert.That(grate.Raycast(new Ray(new Vector3(0f, 1.75f, 28f), Vector3.back), out _, 10f), Is.True,
                "Lowering the visible gate must also lower its collider");
        }
    }
}
