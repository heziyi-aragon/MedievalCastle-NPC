using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using CastleGame = MedievalCastle.Mechanics.CastleGame;

namespace MedievalCastle.Npc.Demo.Tests
{
    public sealed class NpcGameplaySessionTests
    {
        private GameObject host;
        private NpcDialogueService dialogue;
        private CastleGame game;
        private NpcGameplaySession gameplay;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Live player state test");
            dialogue = host.AddComponent<NpcDialogueService>();
            dialogue.Mode = NpcProviderMode.Offline;
            game = new CastleGame(30, 3);
            gameplay = new NpcGameplaySession(game);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [TestCase(false)]
        [TestCase(true)]
        public void PurchaseChangesWhatTheGuardCanVerify(bool chinese)
        {
            var before = gameplay.Snapshot();
            Assert.That(before.coins, Is.EqualTo(30));
            Assert.That(before.hasPass, Is.False);
            Assert.That(before.inventory, Is.Empty);
            Assert.That(before.catalog.Select(item => item.itemId),
                Is.EqualTo(new[] { CastleGame.PassId, CastleGame.PotionId }));

            string guardPrompt = NpcDemoScenario.Prompt(0, 0, chinese);
            var denied = Send("guard", guardPrompt, chinese);
            Assert.That(denied.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            string refusal = gameplay.Apply(dialogue, "guard", denied, NpcAction.None, chinese);
            Assert.That(gameplay.Snapshot().passVerified, Is.False);

            var proposed = Send("merchant", NpcDemoScenario.Prompt(1, 0, chinese), chinese);
            Assert.That(proposed.Decision.itemId, Is.EqualTo(CastleGame.PassId));
            gameplay.Apply(dialogue, "merchant", proposed, NpcAction.RequestPurchase, chinese);
            gameplay.Apply(dialogue, "merchant", proposed, NpcAction.RequestPurchase, chinese);
            var after = gameplay.Snapshot();
            Assert.That(after.coins, Is.EqualTo(20));
            Assert.That(after.hasPass, Is.True);
            Assert.That(after.inventory.Single().quantity, Is.EqualTo(1));
            Assert.That(after.playerId, Is.EqualTo(before.playerId));

            var recorder = new Recorder("{\"reply\":\"Let me inspect your pass.\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
            dialogue.SetProviderForTesting(recorder);
            var accepted = Send("guard", guardPrompt, chinese);
            Assert.That(recorder.Request.PlayerText, Is.EqualTo(guardPrompt));
            Assert.That(recorder.Request.State.hasPass, Is.True);
            Assert.That(recorder.Request.State.coins, Is.EqualTo(20));
            Assert.That(recorder.Request.Messages.Last().content, Does.Contain(refusal));
            gameplay.Apply(dialogue, "guard", accepted, NpcAction.None, chinese);
            Assert.That(gameplay.Snapshot().passVerified, Is.True);
        }

        [Test]
        public void AQuoteOrAnUnconfirmedPurchaseDoesNotSpendCoins()
        {
            var quote = Send("merchant", NpcDemoScenario.Prompt(1, 1, false));
            Assert.That(quote.Decision.Action, Is.EqualTo(NpcAction.None));
            Assert.That(quote.Reply, Does.Contain("8"));
            gameplay.Apply(dialogue, "merchant", quote, NpcAction.None, false);
            var proposed = Send("merchant", "Buy one Castle Pass.");
            Assert.That(proposed.Decision.Action, Is.EqualTo(NpcAction.RequestPurchase));
            gameplay.Apply(dialogue, "merchant", proposed, NpcAction.None, false);

            var anotherItem = Send("merchant", "Buy one herbal tonic.");
            Assert.That(anotherItem.Decision.itemId, Is.EqualTo(CastleGame.PotionId));
            gameplay.Apply(dialogue, "merchant", anotherItem, NpcAction.RequestPurchase, false);
            Assert.That(gameplay.Snapshot().coins, Is.EqualTo(30));
            Assert.That(gameplay.Snapshot().inventory, Is.Empty);
        }

        [Test]
        public void TrainingReadsCurrentProgressAndAReviewDoesNotAwardTheReward()
        {
            string training = NpcDemoScenario.Prompt(2, 0, false);
            var prematureOffer = Send("knight", training);
            gameplay.Apply(dialogue, "knight", prematureOffer, NpcAction.OfferTraining, false);
            Assert.That(gameplay.Snapshot().trainingStatus, Is.EqualTo("available"));

            var order = Send("merchant", NpcDemoScenario.Prompt(1, 0, false));
            gameplay.Apply(dialogue, "merchant", order, NpcAction.RequestPurchase, false);
            var check = Send("guard", NpcDemoScenario.Prompt(0, 0, false));
            gameplay.Apply(dialogue, "guard", check, NpcAction.None, false);
            var offer = Send("knight", training);
            gameplay.Apply(dialogue, "knight", offer, NpcAction.OfferTraining, false);
            Assert.That(game.VisitTrainingPoint(0), Is.True);

            var review = Send("knight", NpcDemoScenario.Prompt(2, 1, false));
            Assert.That(review.Decision.Action, Is.EqualTo(NpcAction.ReviewTraining));
            Assert.That(gameplay.Apply(dialogue, "knight", review, NpcAction.None, false), Does.Contain("1/3"));
            Assert.That(game.VisitTrainingPoint(1), Is.True);
            Assert.That(game.VisitTrainingPoint(2), Is.True);
            review = Send("knight", NpcDemoScenario.Prompt(2, 1, false));
            gameplay.Apply(dialogue, "knight", review, NpcAction.None, false);
            Assert.That(gameplay.Snapshot().trainingStatus, Is.EqualTo("ready"));
            Assert.That(gameplay.Snapshot().coins, Is.EqualTo(20));
            Assert.That(gameplay.Snapshot().reputation, Is.Zero);
        }

        private NpcTurnResult Send(string npc, string text, bool chinese = false)
        {
            var request = dialogue.SendTurnAsync(npc, text, gameplay.Snapshot(), chinese ? "zh" : "en");
            Assert.That(request.IsCompleted, Is.True, "The in-process provider should complete synchronously.");
            var reply = request.GetAwaiter().GetResult();
            Assert.That(reply.Success, Is.True, reply.Error);
            return reply;
        }

        private sealed class Recorder : INpcDialogueProvider
        {
            private readonly string reply;
            internal NpcProviderRequest Request { get; private set; }

            internal Recorder(string reply) => this.reply = reply;

            public Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(reply);
            }
        }
    }
}
