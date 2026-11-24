using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class ConversationSelectionTests : CastleWalkFixture
    {
        internal sealed class ControlledProvider : INpcDialogueProvider
        {
            public readonly List<NpcProviderRequest> Requests = new List<NpcProviderRequest>();
            public readonly List<TaskCompletionSource<string>> Replies = new List<TaskCompletionSource<string>>();

            public Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var pending = new TaskCompletionSource<string>();
                Replies.Add(pending);
                // A slow server can finish after we leave a conversation.
                return pending.Task;
            }
        }

        private const string ValidPass = "{\"reply\":\"Show your pass.\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}";

        [UnityTest, Category("Desktop")]
        public IEnumerator KeyboardEventsWalkGreetAndToggleSettingsThroughTheGameLoop()
        {
            var provider = new ControlledProvider();
            Service.SetProviderForTesting(provider);
            var priorKeyboard = Keyboard.current;
            var originalSettings = InputSystem.settings;
            InputSettings batchSettings = null;
            Keyboard keyboard = null;
            try
            {
#if UNITY_EDITOR
                // A headless editor has no focused Game View to receive queued keyboard events.
                if (Application.isBatchMode)
                {
                    batchSettings = Object.Instantiate(originalSettings);
                    batchSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    batchSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings = batchSettings;
                }
#endif
                keyboard = InputSystem.AddDevice<Keyboard>("CastleWalkTestKeyboard");
                keyboard.MakeCurrent();
                IEnumerator KeyFrame(params Key[] pressed)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressed));
                    yield return null;
                    yield return null;
                }
                yield return KeyFrame();
                var start = Motor.transform.position;
                yield return KeyFrame(Key.W);
                Assert.That(keyboard.wKey.isPressed, Is.True);
                // The production motor ignores keyboard movement when the application lacks focus.
                if (Application.isFocused)
                {
                    float deadline = Time.realtimeSinceStartup + 1.5f;
                    while (Flat(Motor.transform.position - start).magnitude < .06f && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(Vector3.Dot(Flat(Motor.transform.position - start), Flat(Eye.transform.forward).normalized),
                        Is.GreaterThan(.04f), "Held W should advance the actual player in normal Update frames.");
                }
                yield return KeyFrame();
                yield return Approach("guard");
                yield return KeyFrame(Key.E);
                Assert.That(Panel.InConversation, Is.True);
                Assert.That(Panel.SelectedRole, Is.Zero);
                yield return KeyFrame();
                yield return KeyFrame(Key.Escape);
                Assert.That(Panel.MenuOpen, Is.True);
                yield return KeyFrame();
                yield return KeyFrame(Key.Escape);
                Assert.That(Panel.MenuOpen, Is.False);
                Assert.That(Panel.SelectedRole, Is.Zero);
                Assert.That(Panel.LastTurn, Is.Null);
                Assert.That(provider.Requests, Is.Empty);
                yield return KeyFrame();
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (batchSettings)
                {
                    InputSystem.settings = originalSettings;
                    Object.Destroy(batchSettings);
                }
                if (priorKeyboard != null && priorKeyboard.added) priorKeyboard.MakeCurrent();
            }
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator WalkingFromTheSharedSpawnReachesEveryRoleAndEndingTalkRestoresMovement()
        {
            var expected = new[] { NpcAction.CheckPass, NpcAction.RequestPurchase, NpcAction.OfferTraining };
            Assert.That(Panel.Nearby, Is.Null);
            Assert.That(Panel.ExploreText(false), Is.Empty);
            Assert.That(Panel.ExploreText(true), Is.Empty);
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.InConversation, Is.False);
            for (int i = 0; i < expected.Length; i++)
            {
                string role = NpcDemoScenario.Roles[i];
                yield return Approach(role);
                Assert.That(Flat(Eye.transform.position - Spawn.position).magnitude, Is.GreaterThan(.7f));
                Assert.That(Panel.ExploreText(false), Is.EqualTo("E · Talk to " + NpcDemoScenario.RoleName(i, false)));
                Assert.That(Panel.SelectedRole, Is.EqualTo(-1), "Walking near a character alone must not select them.");
                Panel.HandleDesktopFrame(Eye, true, false);
                Assert.That(Panel.SelectedRole, Is.EqualTo(i));
                Assert.That(Panel.InConversation, Is.True);
                Panel.HandleDesktopFrame(Eye, true, false);
                Assert.That(Panel.LastTurn, Is.Null, "Greeting must not spend a turn.");
                var standing = Motor.transform.position;
                for (int frame = 0; frame < 30; frame++) Motor.Step(Vector2.up, 0f, 1f / 60f);
                Assert.That(Flat(standing - Motor.transform.position).magnitude, Is.LessThan(.02f));

                Panel.Submit(NpcDemoScenario.Prompt(i, 0, false));
                Assert.That(Panel.LastTurn, Is.Not.Null);
                Assert.That(Panel.LastTurn.Success && Panel.LastTurn.IsOffline, Is.True);
                Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(expected[i]));
                var jaw = Joint(Actor(role), "Jaw");
                Vector3 rest = jaw.localPosition;
                float travel = 0f;
                yield return Observe(.42f, () => travel = Mathf.Max(travel, Vector3.Distance(rest, jaw.localPosition)));
                Assert.That(travel, Is.GreaterThan(.00019f), role + " speaks");
                Panel.EndTalk();
                Assert.That(Panel.InConversation, Is.False);
                Assert.That(Panel.ExploreText(false), Is.EqualTo("E · Talk to " + NpcDemoScenario.RoleName(i, false)));
                Assert.That(Panel.ExploreText(true), Is.EqualTo("A · Talk to " + NpcDemoScenario.RoleName(i, false)));
                yield return ReturnToSpawn();
                Assert.That(Panel.ExploreText(false), Is.Empty);
                Assert.That(Panel.ExploreText(true), Is.Empty);
                if (i == 0)
                {
                    Panel.SetLanguage(true);
                    Assert.That(Panel.ExploreText(true), Is.Empty);
                    yield return Approach("guard");
                    Assert.That(Panel.ExploreText(true), Is.EqualTo("A · 与守卫交谈"));
                    Assert.That(Panel.ExploreText(false), Is.EqualTo("E · 与守卫交谈"));
                    yield return ReturnToSpawn();
                    Panel.SetLanguage(false);
                }
            }
        }

        [UnityTest]
        public IEnumerator MarketAndTrainingFlankTheGateAndThePlayerCanWalkUnderTheRaisedGrate()
        {
            var landmarks = Castle.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>()).ToArray();
            var grate = landmarks.Single(t => t.name == "Portcullis").GetComponent<BoxCollider>();
            var stall = landmarks.Single(t => t.name == "MarketStall");
            var yard = landmarks.Single(t => t.name == "TrainingYard");
            var merchant = Actor("merchant").transform.position;
            var knight = Actor("knight");
            var guard = Actor("guard").transform.position;

            Assert.That(Vector3.Dot(merchant - Spawn.position, Spawn.right), Is.LessThan(-5f));
            Assert.That(Vector3.Dot(knight.transform.position - Spawn.position, Spawn.right), Is.GreaterThan(5f));
            Assert.That(Flat(merchant - stall.position).magnitude, Is.LessThan(5f), "Shopkeeper at the market stall");
            var fences = landmarks.Where(t => t.parent == yard && (t.name == "Fence1" || t.name == "Fence2"))
                .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>()).ToArray();
            Assert.That(fences, Is.Not.Empty, "The training yard must have visible fence segments.");
            // Castle_Exterior's wall-side row is the row with the lowest world Z value.
            float outsideEdge = fences.Min(f => f.bounds.min.z);
            var meshes = knight.GetComponentsInChildren<MeshRenderer>();
            Assert.That(meshes, Is.Not.Empty);
            float innerEdge = meshes.Max(m => m.bounds.max.z);
            Assert.That(innerEdge, Is.LessThan(outsideEdge - .3f), "The rider, head, tail and hooves must all clear the fence.");
            Assert.That(Vector3.Angle(Flat(guard - Spawn.position), Spawn.forward), Is.LessThan(25f),
                "The guard must be ahead when facing the castle");

            var gate = grate.bounds.center;
            var inside = gate + Spawn.forward * 1.5f;
            yield return WalkTo(inside, () => Flat(inside - Eye.transform.position).magnitude < .20f,
                "the middle of the gateway past the guard and raised grate");
            yield return Approach("knight");
            Assert.That(Motor.GetComponent<CharacterController>().bounds.max.z, Is.LessThan(outsideEdge),
                "A player arriving from the gate can speak from outside the fence.");
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.SelectedRole, Is.EqualTo(2));
            Assert.That(Panel.transform.Find("NPC questions").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator NearestRoleStaysStableAcrossSmallMovementsBesideTwoCharacters()
        {
            var guard = Actor("guard");
            var merchant = Actor("merchant");
            var g = guard.GetComponent<CapsuleCollider>();
            var m = merchant.GetComponent<CapsuleCollider>();
            // The additive test scene is discarded after this case. Recreate two adjacent NPCs
            // on the open approach without depending on their authored market and gate stations.
            var midpoint = Spawn.position - Spawn.forward * 2f;
            guard.transform.position = midpoint - Spawn.right * .95f;
            merchant.transform.position = midpoint + Spawn.right * .95f;
            Physics.SyncTransforms();
            var probe = new GameObject("Proximity sample near the guard and merchant");
            SceneManager.MoveGameObjectToScene(probe, Castle);
            // Stand in front of the pair, then cross their equal-distance line in one-centimetre steps.
            var from = g.bounds.center;
            var to = m.bounds.center;
            var right = Flat(to - from).normalized;
            var front = Vector3.Cross(right, Vector3.up).normalized;
            if (Vector3.Dot(front, Spawn.position - (from + to) * .5f) < 0) front = -front;
            var first = Vector3.Lerp(from, to, .30f) + front * .72f;
            var last = Vector3.Lerp(from, to, .88f) + front * .72f;
            first.y = last.y = (guard.transform.position.y + merchant.transform.position.y) * .5f + 1.6f;
            bool sawSmallLead = false, switched = false;
            for (int i = 0; i <= 100; i++)
            {
                probe.transform.position = Vector3.Lerp(first, last, i / 100f);
                Panel.UpdateNearby(probe.transform);
                if (i == 0) Assert.That(Panel.Nearby, Is.SameAs(guard));
                Vector3 chest = probe.transform.position - Vector3.up * .35f;
                float guardDistance = Flat(g.ClosestPoint(chest) - chest).magnitude;
                float merchantDistance = Flat(m.ClosestPoint(chest) - chest).magnitude;
                float lead = guardDistance - merchantDistance;
                if (lead > .025f && lead < .13f)
                {
                    sawSmallLead = true;
                    Assert.That(Panel.Nearby, Is.SameAs(guard), "Small tracking motion must not flicker between names.");
                }
                if (lead > .22f && Panel.Nearby == merchant) switched = true;
            }
            Assert.That(sawSmallLead && switched, Is.True, "The closer merchant should take over after a clear lead.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeReturnsToTheSameSpeakerAndTheLanguageChoiceSurvivesClosingSettings()
        {
            yield return Approach("merchant");
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.SelectedRole, Is.EqualTo(1));
            Panel.HandleDesktopFrame(Eye, false, true);
            Assert.That(Panel.MenuOpen, Is.True);
            Panel.SetLanguage(true);
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.MenuOpen, Is.True);
            Assert.That(Panel.LastTurn, Is.Null);
            Panel.HandleDesktopFrame(Eye, false, true);
            Assert.That(Panel.MenuOpen, Is.False);
            Assert.That(Panel.InConversation, Is.True);
            Assert.That(Panel.SelectedRole, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(LanguageKey), Is.EqualTo("zh"));
            Panel.Submit(NpcDemoScenario.Prompt(1, 1, true));
            Assert.That(Panel.LastTurn.Reply, Does.Match("[\u4e00-\u9fff]"));
            string path = Castle.path;
            yield return SceneManager.UnloadSceneAsync(Castle);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Castle = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var reopened = Castle.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcDemoPanel>()).Single();
            Assert.That(reopened.Chinese, Is.True, "A new Castle_Exterior session must restore the saved language.");
            Assert.That(reopened.MenuOpen || reopened.InConversation, Is.False);
            Assert.That(reopened.ExploreText(false), Is.Empty);
            Assert.That(reopened.ExploreText(true), Is.Empty);
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator OpeningSettingsFocusesTheSavedLanguageUntilThePointerMovesOrClicks()
        {
            Panel.enabled = false;
            var priorMouse = Mouse.current;
            var mouse = InputSystem.AddDevice<Mouse>("CastleMenuTestMouse");
            try
            {
                mouse.MakeCurrent();
                InputState.Change(mouse.position, Vector2.zero);
                Panel.HandleDesktopFrame(Eye, false, true);
                Canvas.ForceUpdateCanvases();
                var sheet = GameObject.Find("Castle language settings").transform.Find("Parchment");
                var english = sheet.Find("Language row 0").GetComponent<UnityEngine.UI.Image>();
                var chinese = sheet.Find("Language row 1").GetComponent<UnityEngine.UI.Image>();
                var en = RectTransformUtility.WorldToScreenPoint(null, english.rectTransform.position);
                var zh = RectTransformUtility.WorldToScreenPoint(null, chinese.rectTransform.position);
                Color focus = english.color;
                Assert.That(chinese.color, Is.Not.EqualTo(focus));
                Panel.HandleDesktopFrame(Eye, false, true);
                InputState.Change(mouse.position, zh);
                Panel.HandleDesktopFrame(Eye, false, true);
                Panel.HandleDesktopFrame(Eye, false, false);
                Assert.That(english.color, Is.EqualTo(focus), "A stationary pointer cannot replace the opening focus.");
                Assert.That(Panel.SampleMenuPointer(zh + Vector2.right * 2f, false, false), Is.True);
                Assert.That(chinese.color, Is.EqualTo(focus));
                Assert.That(Panel.Chinese, Is.False);
                Panel.SampleMenuPointer(zh, true, false);
                Panel.SampleMenuPointer(zh, false, true);
                Assert.That(Panel.MenuOpen && Panel.Chinese, Is.True);
                Assert.That(sheet.Find("Title").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("设置"));
                Panel.HandleDesktopFrame(Eye, false, true);
                InputState.Change(mouse.position, en);
                Panel.HandleDesktopFrame(Eye, false, true);
                Panel.HandleDesktopFrame(Eye, false, false);
                Assert.That(chinese.color, Is.EqualTo(focus));
                Panel.SampleMenuPointer(en, true, false);
                Panel.SampleMenuPointer(en, false, true);
                Assert.That(Panel.Chinese, Is.False);
                Assert.That(english.color, Is.EqualTo(focus));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                if (priorMouse != null && priorMouse.added) priorMouse.MakeCurrent();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SolidWallBlocksBothWalkingAndCloseRangeConversation()
        {
            yield return Approach("guard");
            var body = Actor("guard").GetComponent<CapsuleCollider>();
            var feet = Motor.transform.position;
            var line = Flat(body.bounds.center - feet).normalized;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, Castle);
            wall.name = "Test gate barrier";
            wall.transform.position = feet + line * .8f + Vector3.up * 1.25f;
            wall.transform.rotation = Quaternion.LookRotation(line);
            wall.transform.localScale = new Vector3(5f, 3f, .12f);
            Physics.SyncTransforms();
            Panel.UpdateNearby(Eye.transform);
            Assert.That(Panel.Nearby, Is.Not.SameAs(Actor("guard")), "Within-distance guard behind a wall must be hidden.");
            Panel.BeginNearby(Eye.transform);
            Assert.That(Panel.SelectedRole, Is.Not.EqualTo(0), "No interaction with the occluded guard.");
            if (Panel.InConversation) Panel.EndTalk();
            for (int i = 0; i < 120; i++) StepToward(body.bounds.center);
            var controller = Motor.GetComponent<CharacterController>();
            var displacement = Vector3.Dot(Flat(Motor.transform.position - feet), line);
            Assert.That(displacement, Is.GreaterThan(.08f), "Input should move the player up to the wall.");
            Assert.That(displacement + controller.radius, Is.LessThanOrEqualTo(.8f + controller.skinWidth),
                "Holding forward crossed the wall surface.");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Panel.UpdateNearby(Eye.transform);
            Assert.That(Panel.Nearby, Is.SameAs(Actor("guard")));
            Begin("guard");
        }

        [UnityTest]
        public IEnumerator InvalidReplyCanBeRetriedOnceAndCanceledLateReplyCannotChangeTheNextSpeaker()
        {
            var provider = new ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Begin("guard");
            var jaw = Joint(Actor("guard"), "Jaw");
            var closed = jaw.localPosition;
            const string question = "May I pass through the gate?";
            Panel.Submit(question);
            Panel.Submit(question);
            Panel.Retry();
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            Assert.That(Panel.LastTurn, Is.Null);
            yield return Observe(.22f, () => Assert.That(Vector3.Distance(closed, jaw.localPosition), Is.LessThan(.0001f)));
            provider.Replies[0].SetResult("{\"action\":\"check_pass\"}");
            yield return WaitForResult();
            Assert.That(Panel.LastTurn.Success, Is.False);
            Assert.That(Panel.LastTurn.Decision, Is.Null);
            yield return Observe(.15f, () => Assert.That(Vector3.Distance(closed, jaw.localPosition), Is.LessThan(.0001f)));

            Panel.Retry();
            Panel.Retry();
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            var retried = provider.Requests[1];
            Assert.That(retried.NpcId, Is.EqualTo("guard"));
            Assert.That(retried.PlayerText, Is.EqualTo(question));
            Assert.That(retried.Language, Is.EqualTo(provider.Requests[0].Language));
            Assert.That(JsonUtility.ToJson(retried.State), Is.EqualTo(JsonUtility.ToJson(provider.Requests[0].State)));
            provider.Replies[1].SetResult(ValidPass);
            yield return WaitForResult();
            Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Panel.Submit(question);
            Assert.That(provider.Requests, Has.Count.EqualTo(3));
            Panel.EndTalk();
            yield return ReturnToSpawn();
            yield return Approach("merchant");
            Begin("merchant");
            provider.Replies[2].SetResult(ValidPass);
            yield return Observe(.22f, () =>
            {
                Assert.That(Panel.LastTurn, Is.Null);
                Assert.That(Panel.SelectedRole, Is.EqualTo(1));
            });
        }

        private IEnumerator WaitForResult()
        {
            float until = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(Panel.LastTurn, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator UnselectedCharactersScanConsultTheLedgerAndSwishTheTail()
        {
            var guard = Actor("guard");
            var merchant = Actor("merchant");
            var knight = Actor("knight");
            var joints = new[] { Joint(guard, "Head"), Joint(guard, "Chest"), Joint(guard, "OffArm"),
                Joint(merchant, "Head"), Joint(merchant, "OffArm"), Joint(knight, "Head"),
                Joint(knight, "EarL"), Joint(knight, "EarR"), Joint(knight, "HorseTail") };
            var start = joints.Select(j => j.localRotation).ToArray();
            var travel = new float[joints.Length];
            var minimum = new[] { 14f, 3f, 3f, 9f, 8f, 6f, 3f, 3f, 7f };
            var jaws = new[] { Joint(guard, "Jaw"), Joint(merchant, "Jaw"), Joint(knight, "Jaw") };
            var closed = jaws.Select(j => j.localPosition).ToArray();
            yield return Observe(12.6f, () =>
            {
                for (int i = 0; i < joints.Length; i++)
                    travel[i] = Mathf.Max(travel[i], Quaternion.Angle(start[i], joints[i].localRotation));
                for (int i = 0; i < jaws.Length; i++)
                    Assert.That(Vector3.Distance(jaws[i].localPosition, closed[i]), Is.LessThan(.0001f));
            });
            for (int i = 0; i < joints.Length; i++)
                Assert.That(travel[i], Is.GreaterThan(minimum[i]), joints[i].name);
            Assert.That(Panel.InConversation, Is.False);
        }

        [UnityTest]
        public IEnumerator ValidatedOfflineDecisionsDriveEachRolesGestureAndPreserveHeldEquipment()
        {
            void Ask(int role, int prompt, NpcAction expected)
            {
                Panel.Submit(NpcDemoScenario.Prompt(role, prompt, false));
                var turn = Panel.LastTurn;
                Assert.That(turn, Is.Not.Null);
                Assert.That(turn.Success && turn.IsOffline, Is.True);
                Assert.That(turn.Decision.Action, Is.EqualTo(expected));
            }

            var guard = Actor("guard");
            var freeHand = Joint(guard, "GestureArm");
            var shield = Joint(guard, "OffArm");
            float handReach = 0f, shieldTravel = 0f;
            yield return Approach("guard");
            Begin("guard");
            yield return new WaitForSecondsRealtime(1.35f);
            Quaternion handRest = freeHand.localRotation, shieldRest = shield.localRotation;
            Ask(0, 0, NpcAction.CheckPass);
            yield return Observe(.7f, () =>
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
            float quoteHand = 0f, quoteLedger = 0f, orderHand = 0f, orderLedger = 0f;
            Panel.EndTalk();
            yield return ReturnToSpawn();
            yield return Approach("merchant");
            Begin("merchant");
            // Let the welcome gesture finish so the samples measure the quote and the order.
            yield return new WaitForSecondsRealtime(1.4f);
            Quaternion presentingRest = presentingArm.localRotation, ledgerRest = ledgerArm.localRotation;
            Ask(1, 1, NpcAction.None);
            yield return Observe(.8f, () =>
            {
                quoteHand = Mathf.Max(quoteHand, Quaternion.Angle(presentingRest, presentingArm.localRotation));
                quoteLedger = Mathf.Max(quoteLedger, Quaternion.Angle(ledgerRest, ledgerArm.localRotation));
            });
            Ask(1, 0, NpcAction.RequestPurchase);
            // Allow the raised ledger to travel from the previous quote to the new action.
            yield return new WaitForSecondsRealtime(.32f);
            yield return Observe(.53f, () =>
            {
                orderHand = Mathf.Max(orderHand, Quaternion.Angle(presentingRest, presentingArm.localRotation));
                orderLedger = Mathf.Max(orderLedger, Quaternion.Angle(ledgerRest, ledgerArm.localRotation));
            });
            Assert.That(quoteLedger, Is.GreaterThan(6f), "The merchant should consult the ledger when quoting a price.");
            Assert.That(quoteLedger - orderLedger, Is.GreaterThan(3f));
            Assert.That(orderHand - quoteHand, Is.GreaterThan(6f), "The purchase request should trigger a distinct gesture.");

            Panel.EndTalk();
            yield return ReturnToSpawn();
            yield return Approach("knight");
            Begin("knight");
            var knight = Actor("knight");
            var head = Joint(knight, "Head");
            var reinArm = Joint(knight, "GestureArm");
            var torso = Joint(knight, "Chest");
            // Read the target assigned in Castle_Exterior, including on player test builds.
            var yard = typeof(NpcDemoPanel).GetField("trainingTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(Panel) as Transform;
            Assert.That(yard, Is.Not.Null, "The knight needs a training target assigned in the scene");
            Assert.That(yard.name, Is.EqualTo("TrainingDummy_B"));
            Quaternion reinRest = reinArm.rotation, torsoRest = torso.rotation;
            float reinTravel = 0f, torsoTravel = 0f;
            Vector3 offerHeading = Vector3.zero, reviewHeading = Vector3.zero;
            void CheckHands()
            {
                reinTravel = Mathf.Max(reinTravel, Quaternion.Angle(reinRest, reinArm.rotation));
                torsoTravel = Mathf.Max(torsoTravel, Quaternion.Angle(torsoRest, torso.rotation));
            }
            Ask(2, 0, NpcAction.OfferTraining);
            yield return Observe(.55f, CheckHands);
            yield return Observe(.35f, () => { CheckHands(); offerHeading += head.forward; });
            // The game owns training state. Validate a review against an active snapshot independently
            // of developer UI, then feed that validated action to the same character presenter.
            var active = NpcDemoScenario.MakeState();
            active.trainingStatus = "active";
            var review = Service.SendTurnAsync("knight", NpcDemoScenario.Prompt(2, 1, false), active, "en");
            Assert.That(review.IsCompleted, Is.True);
            var reviewed = review.GetAwaiter().GetResult();
            Assert.That(reviewed.Success && reviewed.IsOffline, Is.True);
            Assert.That(reviewed.Decision.Action, Is.EqualTo(NpcAction.ReviewTraining));
            knight.Speak(reviewed.Decision.Action, Eye.transform, yard.position + Vector3.up);
            yield return Observe(.65f, CheckHands);
            yield return Observe(.35f, () => { CheckHands(); reviewHeading += head.forward; });

            var toYard = Flat(yard.position - head.position);
            var toPlayer = Flat(Eye.transform.position - head.position);
            float offerError = Vector3.Angle(Flat(offerHeading), toYard);
            float reviewError = Vector3.Angle(Flat(reviewHeading), toPlayer);
            // The visible face is slightly offset from the head bone's forward axis.
            const float maxGazeError = 20f;
            Assert.That(offerError, Is.LessThanOrEqualTo(maxGazeError), "Point out the actual training dummy.");
            Assert.That(reviewError, Is.LessThanOrEqualTo(maxGazeError), "Face the player arriving from the gate.");
            Assert.That(offerError + 8f,
                Is.LessThan(Vector3.Angle(Flat(reviewHeading), toYard)), "The offer should look towards the training yard.");
            Assert.That(reviewError + 8f,
                Is.LessThan(Vector3.Angle(Flat(offerHeading), toPlayer)), "The review should face the player.");
            Assert.That(reinTravel, Is.LessThan(5f), "The gesturing arm should keep hold of the reins.");
            Assert.That(torsoTravel, Is.LessThan(5f), "The torso carries the second hand on the reins.");
        }
    }
}
