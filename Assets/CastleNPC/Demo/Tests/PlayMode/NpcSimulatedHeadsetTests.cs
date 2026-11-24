using System.Collections;
using System.Linq;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    [Category("SimulatedXR")]
    public sealed class NpcSimulatedHeadsetTests
    {
        private Scene castle;
        private GameObject eye;

        [UnityTearDown]
        public IEnumerator UnloadCastle()
        {
            if (eye != null) Object.Destroy(eye);
            if (castle.IsValid() && castle.isLoaded) yield return SceneManager.UnloadSceneAsync(castle);
        }

        [UnityTest]
        public IEnumerator HorseAndRiderGazeFromSpawnSelectOneKnightAndHeldTriggerSendsOneProgressTurn()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Castle_Exterior.unity", LoadSceneMode.Additive);
            castle = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = castle.GetRootGameObjects();
            var panel = roots.SelectMany(root => root.GetComponentsInChildren<NpcDemoPanel>(true)).Single();
            var knight = roots.SelectMany(root => root.GetComponentsInChildren<CastleNpcCharacter>(true))
                .Single(actor => actor.RoleId == "knight");
            Assert.That(panel.isActiveAndEnabled, Is.True);
            // PlayMode supplies sampled frames below, independently of any XR loader or physical controller.
            panel.enabled = false;
            panel.GetComponent<NpcDialogueService>().Mode = NpcProviderMode.Offline;
            eye = new GameObject("Simulated headset camera");
            var camera = eye.AddComponent<Camera>();
            camera.enabled = false;
            Physics.SyncTransforms();
            var spawn = panel.transform.Find("Quest floor position");
            camera.transform.position = spawn.position + Vector3.up * 1.65f;
            void Aim(Vector3 point)
            {
                camera.transform.LookAt(point);
                Assert.That(Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 18f), Is.True);
                Assert.That(hit.collider.GetComponentInParent<CastleNpcCharacter>(), Is.SameAs(knight));
            }
            var rider = knight.GetComponent<CapsuleCollider>();
            Aim(rider.bounds.center + Vector3.up * .19f);
            Assert.That(Physics.Raycast(camera.transform.position, camera.transform.forward, out var riderHit, 18f), Is.True);
            Assert.That(riderHit.collider, Is.SameAs(rider));
            var barrel = knight.transform.Find("Horse barrel").GetComponent<CapsuleCollider>();
            Aim(barrel.bounds.center);
            Assert.That(Physics.Raycast(camera.transform.position, camera.transform.forward, out var horseHit, 18f), Is.True);
            Assert.That(horseHit.collider, Is.SameAs(barrel));

            void Frame(bool down = false, float stick = 0f)
            {
                panel.HandleHeadsetFrame(camera, true, down, false, down ? .95f : 0f, stick);
            }
            Frame();
            var canvas = eye.GetComponentInChildren<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvas.worldCamera, Is.SameAs(camera));
            Text Hud(string name) => canvas.transform.Find(name).GetComponent<Text>();
            string Selection() => Hud("Selection").text;
            Assert.That(Selection(), Does.Contain("Knight"), "Gaze should identify the knight's scene collider.");
            Frame(true);
            Assert.That(Hud("Role and mode").text, Does.Contain("Knight"));
            Assert.That(panel.LastTurn, Is.Null, "The role-selection press must not submit a prompt.");
            yield return null;
            Frame(true);
            Assert.That(panel.LastTurn, Is.Null, "A held role-selection press must not submit a prompt.");
            Frame();

            void Choose(string label, bool release = true)
            {
                for (int i = 0; i < 20 && !Selection().Contains(label); i++)
                {
                    Frame(stick: -1f);
                    Frame();
                }
                Assert.That(Selection(), Does.Contain(label));
                Frame(true);
                if (release) Frame();
            }
            Choose("切换为中文");
            Assert.That(Hud("Role and mode").text, Does.Contain("骑士"));
            Choose("切换训练状态");
            Choose("请检查我的训练进度", false);
            var reply = panel.LastTurn;
            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.Success && reply.IsOffline, Is.True);
            Assert.That(reply.Decision.Action, Is.EqualTo(NpcAction.ReviewTraining));
            Assert.That(Hud("Reply").text, Does.Contain("骑士").And.Contain("离线").And.Contain("0/3"));

            // Keep both digital and analog trigger signals held across two more sampled frames.
            yield return null;
            Frame(true);
            yield return null;
            Frame(true);
            Assert.That(panel.LastTurn.TurnId, Is.EqualTo(reply.TurnId));
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator DesktopPresetsAnimateEachSceneCharacterAndSettleBackToIdle()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Castle_Exterior.unity", LoadSceneMode.Additive);
            castle = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var panel = castle.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<NpcDemoPanel>()).Single();
            panel.GetComponent<NpcDialogueService>().Mode = NpcProviderMode.Offline;
            var actions = new[] { NpcAction.CheckPass, NpcAction.RequestPurchase, NpcAction.OfferTraining };
            var camera = castle.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
            yield return null;
            Assert.That(camera.aspect, Is.EqualTo(camera.pixelRect.width / camera.pixelRect.height).Within(.01f));
            for (int index = 0; index < NpcDemoScenario.Roles.Length; index++)
            {
                string id = NpcDemoScenario.Roles[index];
                var actor = panel.GetComponentsInChildren<CastleNpcCharacter>().Single(a => a.RoleId == id);
                var jaw = actor.GetComponentsInChildren<Transform>().Single(t => t.name == "Jaw");
                var rest = jaw.localPosition;
                var onScreen = camera.WorldToViewportPoint(jaw.position);
                Assert.That(onScreen.z, Is.GreaterThan(0f), id);
                Assert.That(onScreen.x, Is.InRange(.015f, .985f), id);
                Assert.That(onScreen.y, Is.InRange(.015f, .985f), id);
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
                for (int corner = 0; corner < 8; corner++)
                {
                    var b = renderer.bounds;
                    var p = camera.WorldToViewportPoint(new Vector3((corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z));
                    Assert.That(p.x, Is.InRange(0f, 1f), id + " body width");
                    Assert.That(p.y, Is.InRange(0f, 1f), id + " body height");
                }
                panel.SelectRole(index);
                panel.Submit(NpcDemoScenario.Prompt(index, 0, false));
                Assert.That(panel.LastTurn.Success && panel.LastTurn.IsOffline, Is.True, id);
                Assert.That(panel.LastTurn.Decision.Action, Is.EqualTo(actions[index]), id);
                float deadline = Time.realtimeSinceStartup + 1.25f;
                while ((jaw.localPosition - rest).sqrMagnitude < .00000004f && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That((jaw.localPosition - rest).magnitude, Is.GreaterThan(.00019f), id + " speaking");
                yield return new WaitForSecondsRealtime(Mathf.Clamp(panel.LastTurn.Reply.Length * .05f, 1.3f, 5f) + .45f);
                Assert.That(Vector3.Distance(jaw.localPosition, rest), Is.LessThan(.00005f), id + " settled");
            }
            var mount = panel.GetComponentsInChildren<CastleNpcCharacter>().Single(a => a.RoleId == "knight");
            var joints = mount.GetComponentsInChildren<Transform>();
            var ear = joints.Single(t => t.name == "EarL");
            var barrel = joints.Single(t => t.name == "HorseChest");
            var earRest = ear.localRotation;
            var barrelRest = barrel.localScale;
            float maxEar = 0f, maxBreath = 0f;
            float until = Time.realtimeSinceStartup + 5.2f;
            while (Time.realtimeSinceStartup < until && (maxEar < 1f || maxBreath < .001f))
            {
                yield return null;
                maxEar = Mathf.Max(maxEar, Quaternion.Angle(earRest, ear.localRotation));
                maxBreath = Mathf.Max(maxBreath, Vector3.Distance(barrelRest, barrel.localScale));
            }
            Assert.That(maxEar, Is.GreaterThanOrEqualTo(1f), "idle ear flick");
            Assert.That(maxBreath, Is.GreaterThanOrEqualTo(.001f), "idle barrel breathing");
        }
    }
}
