using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class ConversationSelectionTests
    {
        private Scene scene;
        private NpcDemoPanel panel;
        private NpcDialogueService service;
        private Camera sceneCamera;
        private GameObject simulatedEye;
        private ControlledProvider provider;

        private sealed class ControlledProvider : INpcDialogueProvider
        {
            public int Calls { get; private set; }
            public readonly TaskCompletionSource<string> Reply = new TaskCompletionSource<string>();

            public Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken cancellationToken)
            {
                Calls++;
                // Leave the transport alive after cancellation so a genuinely late reply can be exercised.
                return Reply.Task;
            }
        }

        [UnitySetUp]
        public IEnumerator LoadCastle()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Castle_Exterior.unity", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = scene.GetRootGameObjects();
            panel = roots.SelectMany(root => root.GetComponentsInChildren<NpcDemoPanel>()).Single();
            sceneCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
            service = panel.GetComponent<NpcDialogueService>();
            service.Mode = NpcProviderMode.Offline;
            provider = new ControlledProvider();
            service.SetProviderForTesting(provider);
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator UnloadCastle()
        {
            if (service) service.Cancel();
            if (simulatedEye) Object.Destroy(simulatedEye);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private CastleNpcCharacter Actor(string role)
        {
            return panel.GetComponentsInChildren<CastleNpcCharacter>().Single(a => a.RoleId == role);
        }

        private static Transform Joint(CastleNpcCharacter actor, string name)
        {
            return actor.GetComponentsInChildren<Transform>().Single(t => t.name == name);
        }

        private Vector2 ScreenPoint(Collider shape)
        {
            var screen = sceneCamera.WorldToScreenPoint(shape.bounds.center);
            Assert.That(screen.z, Is.Positive, shape.name);
            Assert.That(sceneCamera.pixelRect.Contains(screen), Is.True, shape.name);
            return screen;
        }

        [UnityTest]
        public IEnumerator DesktopClicksSelectTheCastWithoutSubmittingOrRestartingAnActiveGreeting()
        {
            Assert.That(panel.SelectedRole, Is.EqualTo(-1));
            var guard = Actor("guard");
            var merchant = Actor("merchant");
            var knight = Actor("knight");
            var guardPixel = ScreenPoint(guard.GetComponent<CapsuleCollider>());
            var merchantPixel = ScreenPoint(merchant.GetComponent<CapsuleCollider>());
            var horsePixel = ScreenPoint(knight.transform.Find("Horse barrel").GetComponent<CapsuleCollider>());
            var viewport = sceneCamera.pixelRect;
            Assert.That(viewport.xMax, Is.LessThan(Screen.width), "The open desktop panel reserves its own area.");
            var panelPixel = new Vector2((viewport.xMax + Screen.width) * .5f, Screen.height * .5f);
            Assert.That(panel.SelectAtScreenPoint(sceneCamera, panelPixel), Is.False);
            var skyPixel = new Vector2(viewport.xMin + viewport.width * .04f, viewport.yMax - viewport.height * .04f);
            Assert.That(panel.SelectAtScreenPoint(sceneCamera, skyPixel), Is.False);
            Assert.That(panel.SelectedRole, Is.EqualTo(-1));

            // A reference actor gets exactly one greeting. Compare poses at the same times in the same scene.
            var reference = Object.Instantiate(guard, guard.transform.parent);
            foreach (var collider in reference.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in reference.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var head = Joint(guard, "Head");
            var refHead = Joint(reference, "Head");
            var arm = Joint(guard, "GestureArm");
            var refArm = Joint(reference, "GestureArm");
            var startHead = head.localRotation;
            var startRefHead = refHead.localRotation;
            var startArm = arm.localRotation;
            var startRefArm = refArm.localRotation;
            var jaw = Joint(guard, "Jaw");
            var closedJaw = jaw.localPosition;
            Assert.That(panel.SelectAtScreenPoint(sceneCamera, guardPixel), Is.True);
            reference.Greet(sceneCamera.transform);
            Assert.That(panel.SelectedRole, Is.Zero);
            float largestGreeting = 0f;
            float until = Time.unscaledTime + .65f;
            while (Time.unscaledTime < until)
            {
                panel.SelectAtScreenPoint(sceneCamera, guardPixel);
                yield return null;
                Quaternion headMotion = Quaternion.Inverse(startHead) * head.localRotation;
                Quaternion refHeadMotion = Quaternion.Inverse(startRefHead) * refHead.localRotation;
                Quaternion armMotion = Quaternion.Inverse(startArm) * arm.localRotation;
                Quaternion refArmMotion = Quaternion.Inverse(startRefArm) * refArm.localRotation;
                largestGreeting = Mathf.Max(largestGreeting, Quaternion.Angle(Quaternion.identity, headMotion),
                    Quaternion.Angle(Quaternion.identity, armMotion));
                Assert.That(Quaternion.Angle(headMotion, refHeadMotion), Is.LessThan(2f), "Repeated picks restarted the nod.");
                Assert.That(Quaternion.Angle(armMotion, refArmMotion), Is.LessThan(2f), "Repeated picks restarted the arm.");
                Assert.That(Vector3.Distance(closedJaw, jaw.localPosition), Is.LessThan(.0001f));
            }
            Assert.That(largestGreeting, Is.GreaterThan(2f), "The initial guard click must visibly greet the player.");
            panel.SelectAtScreenPoint(sceneCamera, merchantPixel);
            Assert.That(panel.SelectedRole, Is.EqualTo(1));
            panel.SelectAtScreenPoint(sceneCamera, horsePixel);
            Assert.That(panel.SelectedRole, Is.EqualTo(2));
            panel.SelectAtScreenPoint(sceneCamera, guardPixel);
            Assert.That(panel.SelectedRole, Is.Zero);
            Assert.That(panel.LastTurn, Is.Null);
            Assert.That(provider.Calls, Is.Zero);
        }

        [UnityTest, Category("SimulatedXR")]
        public IEnumerator FirstQuestPressOnDefaultGuardGreetsAndHeldPressDoesNotSend()
        {
            var guard = Actor("guard");
            panel.enabled = false;
            simulatedEye = new GameObject("Simulated guard interaction eye");
            var eye = simulatedEye.AddComponent<Camera>();
            eye.enabled = false;
            eye.transform.position = panel.transform.Find("Quest floor position").position + Vector3.up * 1.65f;
            void Frame(bool pressed) => panel.HandleHeadsetFrame(eye, true, pressed, false, pressed ? .95f : 0f, 0f);

            eye.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            Assert.That(Physics.Raycast(eye.transform.position, eye.transform.forward, 18f, ~0,
                QueryTriggerInteraction.Collide), Is.False, "The sky ray must start without an NPC or world hit.");
            Frame(false);
            Frame(true);
            yield return null;
            Frame(false);
            Assert.That(provider.Calls, Is.Zero, "Confirming empty sky must not send the default question.");
            Assert.That(panel.LastTurn, Is.Null);
            Assert.That(panel.SelectedRole, Is.EqualTo(-1));

            eye.transform.LookAt(guard.GetComponent<CapsuleCollider>().bounds.center + Vector3.up * .15f);
            Frame(true);
            Assert.That(panel.SelectedRole, Is.Zero);
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Frame(true);
            }
            Assert.That(provider.Calls, Is.Zero, "Selecting default guard must consume the press, including held frames.");
            Assert.That(panel.LastTurn, Is.Null);
            Frame(false);
            Frame(true);
            Assert.That(provider.Calls, Is.EqualTo(1), "A new press can confirm the selected guard's first question.");
            Frame(true);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Frame(false);
            eye.transform.LookAt(Actor("merchant").GetComponent<CapsuleCollider>().bounds.center + Vector3.up * .15f);
            Frame(true);
            yield return null;
            Assert.That(panel.SelectedRole, Is.Zero);
            Assert.That(service.IsBusy, Is.True, "An attempt to select the merchant must not cancel the guard's pending reply.");
            Assert.That(panel.LastTurn, Is.Null);
        }

        [UnityTest]
        public IEnumerator WaitingInvalidAndLateCanceledRepliesDoNotOpenTheGuardsMouth()
        {
            var guard = Actor("guard");
            var jaw = Joint(guard, "Jaw");
            var closed = jaw.localPosition;
            var otherPixel = ScreenPoint(Actor("merchant").GetComponent<CapsuleCollider>());
            panel.SelectRole(0);
            panel.Submit("May I pass the gate?");
            panel.Submit("May I pass the gate?");
            panel.SelectAtScreenPoint(sceneCamera, otherPixel);
            Assert.That(panel.SelectedRole, Is.Zero, "A pending request keeps its intended speaker.");
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(panel.LastTurn, Is.Null);
            yield return CheckClosed(jaw, closed, .25f);
            provider.Reply.SetResult("{\"action\":\"check_pass\"}");
            yield return AwaitFailure();
            yield return CheckClosed(jaw, closed, .2f);

            provider = new ControlledProvider();
            service.SetProviderForTesting(provider);
            panel.Submit("May I pass the gate?");
            Assert.That(provider.Calls, Is.EqualTo(1));
            service.Cancel();
            yield return AwaitFailure();
            provider.Reply.SetResult("{\"reply\":\"Show your pass.\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
            yield return CheckClosed(jaw, closed, .35f);
            Assert.That(panel.LastTurn.Success, Is.False);
            Assert.That(panel.LastTurn.TurnId, Is.Empty);
        }

        private IEnumerator AwaitFailure()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(panel.LastTurn, Is.Not.Null);
            Assert.That(panel.LastTurn.Success, Is.False);
            Assert.That(panel.LastTurn.Decision, Is.Null);
        }

        private static IEnumerator CheckClosed(Transform jaw, Vector3 rest, float seconds)
        {
            float until = Time.unscaledTime + seconds;
            do
            {
                yield return null;
                Assert.That(Vector3.Distance(rest, jaw.localPosition), Is.LessThan(.0001f));
            } while (Time.unscaledTime < until);
        }

        [UnityTest]
        public IEnumerator ValidatedOfflineDecisionsDriveEachRolesGestureAndPreserveHeldEquipment()
        {
            service.SetProviderForTesting(null);
            void Ask(int role, int prompt, NpcAction expected)
            {
                panel.Submit(NpcDemoScenario.Prompt(role, prompt, false));
                var turn = panel.LastTurn;
                Assert.That(turn, Is.Not.Null);
                Assert.That(turn.Success && turn.IsOffline, Is.True);
                Assert.That(turn.Decision.Action, Is.EqualTo(expected));
            }

            var guard = Actor("guard");
            var freeHand = Joint(guard, "GestureArm");
            var shield = Joint(guard, "OffArm");
            Quaternion handRest = freeHand.localRotation, shieldRest = shield.localRotation;
            float handReach = 0f, shieldTravel = 0f;
            panel.SelectRole(0);
            Ask(0, 0, NpcAction.CheckPass);
            yield return Sample(.7f, () =>
            {
                // Local X pitches the downward-hanging arm forward.
                var delta = Quaternion.Inverse(handRest) * freeHand.localRotation;
                handReach = Mathf.Max(handReach, -Mathf.DeltaAngle(0f, delta.eulerAngles.x));
                shieldTravel = Mathf.Max(shieldTravel, Quaternion.Angle(shieldRest, shield.localRotation));
            });
            Assert.That(handReach, Is.GreaterThan(20f), "The guard should reach to inspect a pass.");
            Assert.That(shieldTravel, Is.LessThan(3f), "The shield should stay at the guard's side.");

            var merchant = Actor("merchant");
            var presentingArm = Joint(merchant, "GestureArm");
            var ledgerArm = Joint(merchant, "OffArm");
            Quaternion presentingRest = presentingArm.localRotation, ledgerRest = ledgerArm.localRotation;
            float quoteHand = 0f, quoteLedger = 0f, orderHand = 0f, orderLedger = 0f;
            panel.SelectRole(1);
            // Let the welcome gesture finish so the samples measure the quote and the order.
            yield return new WaitForSecondsRealtime(1.2f);
            Ask(1, 1, NpcAction.None);
            yield return Sample(.8f, () =>
            {
                quoteHand = Mathf.Max(quoteHand, Quaternion.Angle(presentingRest, presentingArm.localRotation));
                quoteLedger = Mathf.Max(quoteLedger, Quaternion.Angle(ledgerRest, ledgerArm.localRotation));
            });
            Ask(1, 0, NpcAction.RequestPurchase);
            yield return Sample(.85f, () =>
            {
                orderHand = Mathf.Max(orderHand, Quaternion.Angle(presentingRest, presentingArm.localRotation));
                orderLedger = Mathf.Max(orderLedger, Quaternion.Angle(ledgerRest, ledgerArm.localRotation));
            });
            Assert.That(quoteLedger, Is.GreaterThan(6f), "The merchant should consult the ledger when quoting a price.");
            Assert.That(quoteLedger - orderLedger, Is.GreaterThan(3f));
            Assert.That(orderHand - quoteHand, Is.GreaterThan(6f), "The purchase request should trigger a distinct gesture.");

            panel.enabled = false;
            simulatedEye = new GameObject("Player at Quest spawn");
            var eye = simulatedEye.AddComponent<Camera>();
            eye.enabled = false;
            eye.transform.position = panel.transform.Find("Quest floor position").position + Vector3.up * 1.65f;
            var knight = Actor("knight");
            eye.transform.LookAt(knight.GetComponent<CapsuleCollider>().bounds.center + Vector3.up * .19f);
            void Frame(bool pressed = false, float stick = 0f) =>
                panel.HandleHeadsetFrame(eye, true, pressed, false, pressed ? .95f : 0f, stick);
            Frame(); Frame(true); Frame();
            Assert.That(panel.SelectedRole, Is.EqualTo(2));
            var head = Joint(knight, "Head");
            var reinArm = Joint(knight, "GestureArm");
            var torso = Joint(knight, "Chest");
            var yard = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "TrainingDummy_A");
            Quaternion reinRest = reinArm.rotation, torsoRest = torso.rotation;
            float reinTravel = 0f, torsoTravel = 0f;
            Vector3 offerHeading = Vector3.zero, reviewHeading = Vector3.zero;
            void CheckHands()
            {
                reinTravel = Mathf.Max(reinTravel, Quaternion.Angle(reinRest, reinArm.rotation));
                torsoTravel = Mathf.Max(torsoTravel, Quaternion.Angle(torsoRest, torso.rotation));
            }
            Ask(2, 0, NpcAction.OfferTraining);
            yield return Sample(.55f, CheckHands);
            yield return Sample(.35f, () => { CheckHands(); offerHeading += head.forward; });
            // Use the headset menu the player uses to advance the demo state to active.
            var choice = eye.GetComponentInChildren<Canvas>().transform.Find("Selection").GetComponent<Text>();
            for (int i = 0; i < 20 && !choice.text.Contains("Cycle training"); i++) { Frame(stick: -1f); Frame(); }
            Assert.That(choice.text, Does.Contain("Cycle training"));
            Frame(true); Frame();
            Ask(2, 1, NpcAction.ReviewTraining);
            yield return Sample(.65f, CheckHands);
            yield return Sample(.35f, () => { CheckHands(); reviewHeading += head.forward; });

            Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
            var toYard = Flat(yard.position - head.position);
            var toPlayer = Flat(eye.transform.position - head.position);
            Assert.That(Vector3.Angle(Flat(offerHeading), toYard) + 8f,
                Is.LessThan(Vector3.Angle(Flat(reviewHeading), toYard)), "The offer should look towards the training yard.");
            Assert.That(Vector3.Angle(Flat(reviewHeading), toPlayer) + 8f,
                Is.LessThan(Vector3.Angle(Flat(offerHeading), toPlayer)), "The review should face the player.");
            Assert.That(reinTravel, Is.LessThan(5f), "The gesturing arm should keep hold of the reins.");
            Assert.That(torsoTravel, Is.LessThan(5f), "The torso carries the second hand on the reins.");
        }

        private static IEnumerator Sample(float seconds, System.Action observe)
        {
            float until = Time.unscaledTime + seconds;
            do { yield return null; observe(); } while (Time.unscaledTime < until);
        }
    }
}
