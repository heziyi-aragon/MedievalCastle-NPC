using NUnit.Framework;
using UnityEngine;

namespace MedievalCastle.Npc.Demo.Tests
{
    public sealed class NpcDemoPresetTests
    {
        [Test]
        public void WindowsDemoFontCanDisplayChinesePrompts()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) Assert.Ignore("Windows Link font check.");
            var font = NpcDemoPanel.CreateDisplayFont();
            Assert.That(font, Is.Not.Null, "Install a Windows Chinese display font to show localized dialogue.");
            try
            {
                foreach (char glyph in "城堡守卫商人骑士通行证提灯训练")
                    Assert.That(font.HasCharacter(glyph), Is.True, "Missing glyph U+" + ((int)glyph).ToString("X4"));
            }
            finally { Object.DestroyImmediate(font); }
        }

        [TestCase(0, 0, false, "available", NpcAction.CheckPass)]
        [TestCase(0, 0, true, "available", NpcAction.CheckPass)]
        [TestCase(0, 1, true, "available", NpcAction.None)]
        [TestCase(1, 0, true, "available", NpcAction.RequestPurchase)]
        [TestCase(1, 0, false, "available", NpcAction.RequestPurchase)]
        [TestCase(1, 1, false, "available", NpcAction.None)]
        [TestCase(2, 0, false, "available", NpcAction.OfferTraining)]
        [TestCase(2, 1, true, "ready", NpcAction.ReviewTraining)]
        [TestCase(2, 1, false, "rewarded", NpcAction.None)]
        public void VisiblePresetSendsExpectedProposalWithoutApplyingIt(
            int role, int preset, bool chinese, string training, NpcAction expected)
        {
            var obj = new GameObject("NPC demo preset test");
            try
            {
                var service = obj.AddComponent<NpcDialogueService>();
                service.Mode = NpcProviderMode.Offline;
                var state = NpcDemoScenario.MakeState();
                state.trainingStatus = training;
                if (training == "ready" || training == "rewarded") state.trainingProgress = state.trainingTarget;
                var original = JsonUtility.ToJson(state);
                var task = service.SendTurnAsync(NpcDemoScenario.Roles[role],
                    NpcDemoScenario.Prompt(role, preset, chinese), state, chinese ? "zh" : "en");
                Assert.That(task.IsCompleted, Is.True, "Offline demo presets should resolve locally.");
                var result = task.GetAwaiter().GetResult();
                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.IsOffline, Is.True);
                Assert.That(result.Decision.Action, Is.EqualTo(expected));
                Assert.That(result.Reply, Is.Not.Empty);
                Assert.That(result.RawJson, Is.Not.Empty);
                if (expected == NpcAction.RequestPurchase)
                {
                    Assert.That(result.Decision.itemId, Is.EqualTo("bread"));
                    Assert.That(result.Decision.quantity, Is.EqualTo(1));
                }
                if (expected == NpcAction.OfferTraining || expected == NpcAction.ReviewTraining)
                    Assert.That(result.Decision.questId, Is.EqualTo("courtyard_drill"));
                service.ReportDecisionResult(result.TurnId, "rejected");
                Assert.That(JsonUtility.ToJson(state), Is.EqualTo(original));
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
