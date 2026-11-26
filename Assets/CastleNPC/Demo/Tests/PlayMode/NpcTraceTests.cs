using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class NpcTraceTests : CastleWalkFixture
    {
        private const string PurchaseReply =
            "{\"reply\":\"A pass costs ten coins.\",\"action\":\"request_purchase\",\"itemId\":\"castle_pass\",\"quantity\":1,\"questId\":\"\"}";
        private const string CheckPassReply =
            "{\"reply\":\"Show your pass.\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}";

        [UnityTest, Category("Desktop")]
        public IEnumerator SafeRequestRawResponseAndPurchaseConfirmationShareOneTrace()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("merchant");
            Begin("merchant");
            Panel.Submit("I would like one castle pass.");
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            provider.Replies[0].SetResult(PurchaseReply);
            yield return WaitForTurn();

            Assert.That(Panel.LastTurn.Success, Is.True);
            Assert.That(Panel.Traces, Has.Count.EqualTo(1));
            var trace = Panel.Traces[0];
            Assert.That(trace.npcId, Is.EqualTo("merchant"));
            Assert.That(trace.liveProvider, Is.False);
            Assert.That(trace.contextJson, Does.Contain("\"coins\":30").And.Contain("I would like one castle pass."));
            Assert.That(trace.requestJson, Does.Contain("\"response_format\"").And.Contain("\"messages\""));
            Assert.That(trace.requestJson, Does.Not.Contain("Authorization").And.Not.Contain("Bearer")
                .And.Not.Contain("DEEPSEEK_API_KEY"));
            Assert.That(trace.rawJson, Is.EqualTo(PurchaseReply));
            Assert.That(trace.validationAttempted && trace.validationAccepted, Is.True);
            Assert.That(trace.proposedAction, Is.EqualTo("RequestPurchase"));
            Assert.That(trace.executionStatus, Does.Contain("Pending player confirmation"));
            Assert.That(trace.stateDiff, Is.EqualTo("No player state change"));
            Assert.That(trace.afterStateJson, Is.EqualTo(trace.beforeStateJson));

            Panel.FinishPendingPurchase(true);
            Assert.That(Panel.Traces, Has.Count.EqualTo(1));
            Assert.That(Panel.Traces[0], Is.SameAs(trace));
            Assert.That(trace.executionStatus, Does.Contain("Purchase confirmed"));
            Assert.That(trace.executionOutcome, Does.Contain("Balance: 20 coins"));
            Assert.That(trace.stateDiff, Does.Contain("coins: 30 → 20").And.Contain("hasPass: false → true")
                .And.Contain("castle_pass ×1"));
            Assert.That(trace.ToSafeJson(), Does.Contain(PurchaseReply.Replace("\"", "\\\""))
                .And.Not.Contain("Authorization").And.Not.Contain("Bearer"));
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator InvalidModelJsonRemainsVisibleAndIsNotExecuted()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Begin("guard");
            const string invalid = "{\"reply\":";
            Panel.Submit("Open the gate.");
            provider.Replies[0].SetResult(invalid);
            yield return WaitForTurn();

            Assert.That(Panel.LastTurn.Success, Is.False);
            var trace = Panel.Traces[0];
            Assert.That(trace.rawJson, Is.EqualTo(invalid));
            Assert.That(trace.validationAttempted, Is.True);
            Assert.That(trace.validationAccepted, Is.False);
            Assert.That(trace.validationError, Is.Not.Empty);
            Assert.That(trace.executionStatus, Is.EqualTo("Not executed"));
            Assert.That(trace.stateDiff, Is.EqualTo("Not executed"));
            Assert.That(trace.error, Is.Not.Empty);
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator GuardWithoutAPassShowsValidatedProposalButDeniedExecution()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Begin("guard");
            Panel.Submit("Please open the gate.");
            provider.Replies[0].SetResult(CheckPassReply);
            yield return WaitForTurn();

            var trace = Panel.Traces[0];
            Assert.That(trace.validationAccepted, Is.True);
            Assert.That(trace.proposedAction, Is.EqualTo("CheckPass"));
            Assert.That(trace.executionStatus, Is.EqualTo("Unity execution completed"));
            Assert.That(trace.executionOutcome, Is.Not.Empty);
            Assert.That(trace.stateDiff, Is.EqualTo("No player state change"));
            Assert.That(trace.afterStateJson, Does.Contain("\"passVerified\":false"));
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator EndingConversationCancelsThePendingPurchaseTraceWithoutCharging()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("merchant");
            Begin("merchant");
            Panel.Submit("I need a pass.");
            provider.Replies[0].SetResult(PurchaseReply);
            yield return WaitForTurn();
            var trace = Panel.Traces[0];

            Panel.EndTalk();
            Assert.That(trace.executionStatus, Is.EqualTo("Purchase canceled by player"));
            Assert.That(trace.executionOutcome, Is.EqualTo("Conversation ended before purchase confirmation."));
            Assert.That(trace.stateDiff, Is.EqualTo("No player state change"));
            Assert.That(trace.afterStateJson, Does.Contain("\"coins\":30"));
        }

        [UnityTest]
        public IEnumerator TraceHistoryRetainsOnlyTheEightMostRecentTurns()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Begin("guard");
            for (int i = 0; i < 9; i++)
            {
                Panel.Submit("Trace turn " + i);
                provider.Replies[i].SetResult(
                    "{\"reply\":\"Reply " + i + ".\",\"action\":\"none\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
                yield return WaitForTurn();
            }

            Assert.That(Panel.Traces, Has.Count.EqualTo(8));
            Assert.That(Panel.Traces[0].playerText, Is.EqualTo("Trace turn 8"));
            Assert.That(Panel.Traces[7].playerText, Is.EqualTo("Trace turn 1"));
        }

        private IEnumerator WaitForTurn()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Panel.LastTurn, Is.Not.Null, "The controlled provider response was not applied.");
        }
    }
}
