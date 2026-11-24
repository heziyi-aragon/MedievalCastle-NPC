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
            }
            finally
            {
                CastlePlayerMotor.MovementLocked = priorLock;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(floor);
            }
        }

        [TestCase(30)]
        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void SmoothTurningTracksStickStrengthKeepsEyePivotAndResetsAcrossMenus(int fps)
        {
            var root = new GameObject("Sampled turning player");
            bool previousLock = CastlePlayerMotor.MovementLocked;
            try
            {
                root.transform.position = new Vector3(120, 160, 120);
                var eye = new GameObject("Offset eye").AddComponent<Camera>();
                eye.enabled = false;
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(.45f, 1.63f, -.24f);
                var pivot = eye.transform.position;
                var motor = root.AddComponent<CastlePlayerMotor>();
                motor.Configure(eye, true);
                CastlePlayerMotor.MovementLocked = false;
                float Yaw() => root.transform.eulerAngles.y;
                void Sample(Vector2 stick, int count)
                {
                    for (int i = 0; i < count; i++) motor.Step(Vector2.zero, stick, 1f / fps);
                }
                Sample(new Vector2(.12f, .97f), fps);
                Assert.That(Mathf.DeltaAngle(0f, Yaw()), Is.EqualTo(0f).Within(.01f));
                Sample(Vector2.right, 1);
                Assert.That(Yaw(), Is.InRange(.005f, .35f), "Ease in on the first frame.");
                Sample(Vector2.right, fps - 1);
                Assert.That(Yaw(), Is.EqualTo(61.1f).Within(.05f), "65 degrees/s after 0.12s ramp.");
                float beforeRelease = Yaw();
                Sample(Vector2.zero, fps);
                Assert.That(Mathf.DeltaAngle(beforeRelease, Yaw()), Is.EqualTo(2.6f).Within(.04f));
                var worldEye = eye.transform.position;
                Assert.That(Vector2.Distance(new Vector2(worldEye.x, worldEye.z), new Vector2(pivot.x, pivot.z)),
                    Is.LessThan(.01f), "The camera should turn in place even after a physical sidestep.");
                float halfStart = Yaw();
                Sample(Vector2.right * .59f, fps); // halfway between the dead zone and the end of travel
                Assert.That(Mathf.DeltaAngle(halfStart, Yaw()), Is.EqualTo(31.525f).Within(.05f));
                float beforeReverse = Yaw();
                Sample(Vector2.left, 1);
                Assert.That(Mathf.DeltaAngle(beforeReverse, Yaw()), Is.GreaterThan(0f), "Ease through a reversal.");
                Sample(Vector2.left, fps);
                Assert.That(Mathf.DeltaAngle(beforeReverse, Yaw()), Is.LessThan(-50f));
                float paused = Yaw();
                CastlePlayerMotor.MovementLocked = true;
                Sample(Vector2.left, fps);
                Assert.That(Mathf.DeltaAngle(paused, Yaw()), Is.EqualTo(0f).Within(.01f));
                CastlePlayerMotor.MovementLocked = false;
                Sample(Vector2.up, fps / 2);
                Sample(Vector2.left, fps);
                Assert.That(Mathf.DeltaAngle(paused, Yaw()), Is.EqualTo(0f).Within(.01f),
                    "Sliding sideways from a menu selection must not steer until the stick returns to center.");
                Sample(Vector2.zero, 1);
                Sample(Vector2.left, fps);
                Assert.That(Mathf.DeltaAngle(paused, Yaw()), Is.EqualTo(-61.1f).Within(.05f));
            }
            finally
            {
                CastlePlayerMotor.MovementLocked = previousLock;
                Object.DestroyImmediate(root);
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
