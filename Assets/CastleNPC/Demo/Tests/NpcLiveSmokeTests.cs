using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.Tests
{
    [Category("NpcLive")]
    public sealed class NpcLiveSmokeTests
    {
        [UnityTest] public IEnumerator Guard() => RunTurn(0, NpcAction.CheckPass);
        [UnityTest] public IEnumerator Merchant() => RunTurn(1, NpcAction.RequestPurchase);
        [UnityTest] public IEnumerator Knight() => RunTurn(2, NpcAction.OfferTraining);

        private static IEnumerator RunTurn(int index, NpcAction action)
        {
            string optedIn = Environment.GetEnvironmentVariable("CASTLE_NPC_LIVE_TESTS");
#if UNITY_EDITOR_WIN
            if (string.IsNullOrWhiteSpace(optedIn))
                optedIn = Environment.GetEnvironmentVariable("CASTLE_NPC_LIVE_TESTS", EnvironmentVariableTarget.User);
#endif
            if (optedIn != "1") Assert.Ignore("Set CASTLE_NPC_LIVE_TESTS=1 to run live API tests.");
            var obj = new GameObject("Live NPC integration test");
            try
            {
                var service = obj.AddComponent<NpcDialogueService>();
                if (!service.HasApiKey) Assert.Ignore("DEEPSEEK_API_KEY is required for live API tests.");
                service.Mode = NpcProviderMode.DeepSeek;
                var role = NpcDemoScenario.Roles[index];
                var operation = service.SendTurnAsync(role, NpcDemoScenario.Prompt(index, 0, false),
                    NpcDemoScenario.MakeState(), "en");
                while (!operation.IsCompleted) yield return null;
                Assert.That(operation.IsFaulted || operation.IsCanceled, Is.False, "Live turn failed to complete.");
                var result = operation.Result;
                Assert.That(result.IsOffline, Is.False);
                Assert.That(result.Success, Is.True, "The provider did not return a valid decision for " + role + ".");
                Assert.That(result.Reply.Length, Is.GreaterThan(0));
                Assert.That(result.Decision.Action, Is.EqualTo(action));
                Assert.That(result.TurnId, Is.Not.Empty);
                TestContext.WriteLine($"role={role}, provider=DeepSeek, action={result.Decision.Action}, replyLength={result.Reply.Length}");
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
