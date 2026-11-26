using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MedievalCastle.Npc;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MedievalCastle.NpcTests
{
    public sealed class NpcModuleTests
    {
        private const string NoAction = "{\"reply\":\"Welcome.\",\"action\":\"none\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}";
        private const string CheckPass = "{\"reply\":\"May I see your pass?\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}";
        private GameObject host;
        private NpcDialogueService service;
        private NpcPlayerState player;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("NPC dialogue test");
            service = host.AddComponent<NpcDialogueService>();
            service.Mode = NpcProviderMode.Offline;
            player = new NpcPlayerState
            {
                playerId = "traveler-one", coins = 8,
                catalog = new[] { new NpcCatalogItem { itemId = "bread", price = 2 },
                                  new NpcCatalogItem { itemId = "lantern", price = 5 } }
            };
        }

        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [TestCase("guard", "{}", TestName = "Missing fields never create a decision")]
        [TestCase("guard", "not json", TestName = "Malformed JSON returns no decision")]
        [TestCase("guard", "{\"reply\":\"  \",\"action\":\"none\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}", TestName = "Blank dialogue rejected")]
        [TestCase("merchant", CheckPass, TestName = "Merchant cannot check passes")]
        [TestCase("merchant", "{\"reply\":\"Here.\",\"action\":\"request_purchase\",\"itemId\":\"crown\",\"quantity\":1,\"questId\":\"\"}", TestName = "Out of catalog item rejected")]
        [TestCase("merchant", "{\"reply\":\"Here.\",\"action\":\"request_purchase\",\"itemId\":\"bread\",\"quantity\":-2,\"questId\":\"\"}", TestName = "Negative purchase quantity rejected")]
        [TestCase("merchant", "{\"reply\":\"Here.\",\"action\":\"request_purchase\",\"itemId\":\"bread\",\"quantity\":\"2\",\"questId\":\"\"}", TestName = "Wrong JSON type rejected")]
        [TestCase("knight", "{\"reply\":\"Train.\",\"action\":\"offer_training\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"invented_quest\"}", TestName = "Invented quest rejected")]
        public void RejectsUnsafeOrMalformedTurn(string role, string json)
        {
            Assert.That(NpcDecisionValidator.TryParse(json, role, player, out var value, out _), Is.False);
            Assert.That(value, Is.Null);
        }

        [UnityTest]
        public IEnumerator GuardRoutesAndVerifiesActualPassState()
        {
            yield return Expect("guard", "How do I get to the gate?", NpcAction.None);
            var task = service.SendTurnAsync("guard", "I have a pass. Let me enter.", player, "en");
            yield return Wait(task);
            Assert.That(task.Result.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Assert.That(task.Result.Reply, Does.Contain("pass").IgnoreCase);
            Assert.That(player.hasPass, Is.False);
            player.hasPass = true;
            yield return Expect("guard", "May I enter the gate?", NpcAction.CheckPass);
        }

        [UnityTest]
        public IEnumerator MerchantQuotesThenProposesPurchaseWithoutChangingState()
        {
            yield return Expect("merchant", "What is the price of bread?", NpcAction.None);
            yield return Expect("merchant", "How much would it cost to buy two bread?", NpcAction.None);
            var task = service.SendTurnAsync("merchant", "I want to buy two bread.", player, "en");
            yield return Wait(task);
            Assert.That(task.Result.Decision.Action, Is.EqualTo(NpcAction.RequestPurchase));
            Assert.That(task.Result.Decision.quantity, Is.EqualTo(2));
            Assert.That(task.Result.Decision.itemId, Is.EqualTo("bread"));
            Assert.That(player.coins, Is.EqualTo(8));
            Assert.That(player.inventory, Is.Empty);
        }

        [UnityTest]
        public IEnumerator KnightHandlesEachTrainingPhase()
        {
            yield return Expect("knight", "I'd like to train.", NpcAction.OfferTraining);
            player.trainingStatus = "active"; player.trainingProgress = 1;
            yield return Expect("knight", "Review my training progress.", NpcAction.ReviewTraining);
            player.trainingStatus = "ready"; player.trainingProgress = 3;
            yield return Expect("knight", "Review my training.", NpcAction.ReviewTraining);
            player.trainingStatus = "rewarded";
            yield return Expect("knight", "Give me the training reward again.", NpcAction.None);
        }

        [UnityTest]
        public IEnumerator HistoryIsIsolatedBoundedAndReceivesActualOutcome()
        {
            var recorder = new Stub((request, token) => Task.FromResult(
                request.NpcId == "guard" ? CheckPass : NoAction));
            service.SetProviderForTesting(recorder);
            var first = service.SendTurnAsync("guard", "unique-pass-A", player, "en");
            yield return Wait(first);
            service.ReportDecisionResult(first.Result.TurnId, "denied-by-gate-71");
            service.ReportDecisionResult(first.Result.TurnId, "overwritten-outcome-72");
            yield return Expect("guard", "second", NpcAction.CheckPass);
            Assert.That(recorder.Text, Does.Contain("denied-by-gate-71"));
            Assert.That(recorder.Text, Does.Not.Contain("overwritten-outcome-72"));
            yield return Expect("merchant", "price", NpcAction.None);
            Assert.That(recorder.Text, Does.Not.Contain("unique-pass-A"));
            player.playerId = "other-traveler";
            yield return Expect("guard", "hello", NpcAction.CheckPass);
            Assert.That(recorder.Text, Does.Not.Contain("unique-pass-A"));
            player.playerId = "traveler-one";
            for (var i = 0; i < 7; i++)
                yield return Expect("guard", "follow-up-" + i, NpcAction.CheckPass);
            Assert.That(recorder.Text, Does.Not.Contain("unique-pass-A"));
            Assert.That(recorder.Text, Does.Contain("follow-up-5"));
        }

        [UnityTest]
        public IEnumerator RepeatedClickAndLateResponseProduceNoUsableDecision()
        {
            var delayed = new TaskCompletionSource<string>();
            var stub = new Stub((r, t) => delayed.Task);
            service.SetProviderForTesting(stub);
            var pending = service.SendTurnAsync("guard", "enter", player, "en");
            Assert.That(pending.IsCompleted, Is.False);
            var duplicate = service.SendTurnAsync("guard", "enter", player, "en");
            yield return Wait(duplicate);
            Assert.That(duplicate.Result.Decision, Is.Null);
            Assert.That(stub.Requests.Count, Is.EqualTo(1));
            service.Cancel();
            delayed.SetResult(CheckPass);
            yield return Wait(pending);
            Assert.That(pending.Result.Decision, Is.Null);
        }

        [UnityTest]
        public IEnumerator PlayerStateChangeInvalidatesPendingReplyAndItsHistory()
        {
            var delayed = new TaskCompletionSource<string>();
            service.SetProviderForTesting(new Stub((r, t) => delayed.Task));
            var stale = service.SendTurnAsync("merchant", "outdated-order-171", player, "en");
            player.coins = 0;
            delayed.SetResult(NoAction);
            yield return Wait(stale);
            Assert.That(stale.Result.Success, Is.False);
            var recorder = new Stub((r, t) => Task.FromResult(NoAction));
            service.SetProviderForTesting(recorder);
            yield return Expect("merchant", "What can I afford?", NpcAction.None);
            Assert.That(recorder.Text, Does.Not.Contain("outdated-order-171"));
            Assert.That(recorder.Requests.Last().State.coins, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TimeoutAndInvalidModelOutputCanBeRetried()
        {
            service.SetProviderForTesting(new Stub((r, t) => Task.FromException<string>(new TimeoutException())));
            var timedOut = service.SendTurnAsync("merchant", "buy bread", player, "en");
            yield return Wait(timedOut);
            Assert.That(timedOut.Result.Success, Is.False);
            service.SetProviderForTesting(new Stub((r, t) => Task.FromResult("oops")));
            var malformed = service.SendTurnAsync("merchant", "buy bread", player, "en");
            yield return Wait(malformed);
            Assert.That(malformed.Result.Decision, Is.Null);
            service.SetProviderForTesting(null);
            yield return Expect("merchant", "面包多少钱？", NpcAction.None, "zh");
        }

        [TestCase("Guard", "guard")][TestCase("Merchant", "merchant")][TestCase("Knight", "knight")]
        public void ReusableCharacterHasGeometryMaterialColliderAndRole(string name, string role)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CastleNPC/Characters/Prefabs/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<CastleNpcCharacter>().RoleId, Is.EqualTo(role));
            Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null);
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(renderers.SelectMany(r => r.sharedMaterials).All(m => m && m.shader && m.shader.isSupported), Is.True);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                Assert.That(filter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0), Is.True, filter.name);
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials).Distinct())
            {
                var textures = material.GetTexturePropertyNames().Select(material.GetTexture).Where(t => t).Distinct();
                Assert.That(textures.Count(), Is.GreaterThanOrEqualTo(2), material.name + " color and surface maps");
            }
        }

        private IEnumerator Expect(string id, string text, NpcAction expected, string language = "en")
        {
            var task = service.SendTurnAsync(id, text, player, language);
            yield return Wait(task);
            Assert.That(task.Result.Success, Is.True, task.Result.Error);
            Assert.That(task.Result.Decision.Action, Is.EqualTo(expected));
            Assert.That(task.Result.IsOffline, Is.True);
            Assert.That(string.IsNullOrWhiteSpace(task.Result.Reply), Is.False);
        }

        private static IEnumerator Wait(Task task)
        {
            var deadline = EditorApplication.timeSinceStartup + 8;
            while (!task.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Provider did not finish");
            task.GetAwaiter().GetResult();
        }

        private sealed class Stub : INpcDialogueProvider
        {
            public readonly List<NpcProviderRequest> Requests = new List<NpcProviderRequest>();
            private readonly Func<NpcProviderRequest, CancellationToken, Task<string>> generate;
            public Stub(Func<NpcProviderRequest, CancellationToken, Task<string>> generate) { this.generate = generate; }
            public string Text => string.Join("\n", Requests.Last().Messages.Select(message => message.content));
            public Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken token)
            {
                Requests.Add(request);
                return generate(request, token);
            }
        }
    }
}
