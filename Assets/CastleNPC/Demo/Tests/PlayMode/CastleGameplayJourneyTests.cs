using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class CastleGameplayJourneyTests : CastleWalkFixture
    {
        private const string EntryRequest = "May I pass through the gate?";
        private const string ClaimRequest = "Claim my reward.";
        private const string ConfirmReward = "Confirm reward";
        private static readonly Vector3 PathOutsideYard = new Vector3(-7f, 0f, 27.2f);
        private static readonly Vector3 PathIntoYard = new Vector3(-7f, 0f, 34.5f);

        private Transform Questions => Panel.transform.Find("NPC questions");
        private string Status => Questions.Find("Status").GetComponent<Text>().text;

        [UnityTest, Category("Desktop")]
        public IEnumerator ANewPlayerBuysPassEntersCastleAndClaimsTrainingRewardOnce()
        {
            var landmarks = Castle.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>()).ToArray();
            var portcullis = landmarks.Single(t => t.name == "Portcullis").GetComponent<BoxCollider>();
            Vector3 closed = portcullis.transform.localPosition;
            Vector3 gate = portcullis.bounds.center;
            Vector3 outside = gate - Spawn.forward;
            Vector3 inside = gate + Spawn.forward * 1.5f;
            AssertWallet(30, 0, "Backpack empty");

            yield return TalkTo("guard");
            Choose(EntryRequest);
            Assert.That(Status, Does.Contain("need a Castle Pass"));
            Choose("End conversation");
            yield return ReturnToSpawn();
            yield return WalkNear(outside, "the closed castle gate");
            for (int frame = 0; frame < 90; frame++)
            {
                StepToward(inside);
                if (frame % 8 == 7) yield return null;
            }
            Assert.That(Vector3.Dot(Eye.transform.position - gate, Spawn.forward), Is.LessThan(-.12f),
                "The closed portcullis must stop an unverified player.");
            yield return ReturnToSpawn();

            yield return TalkTo("merchant");
            Choose("Buy one Castle Pass.");
            Assert.That(Status, Does.Contain("Castle Pass × 1").And.Contain("Total 10 coins"));
            Choose("Confirm purchase");
            Assert.That(Status, Does.Contain("Balance: 20 coins"));
            Choose("End conversation");
            AssertWallet(20, 0, "Castle Pass ×1");
            Assert.That(portcullis.transform.localPosition, Is.EqualTo(closed),
                "The purchase alone must leave the gate closed.");
            yield return ReturnToSpawn();

            yield return TalkTo("guard");
            Choose(EntryRequest);
            Assert.That(Status, Is.EqualTo("Castle Pass verified."));
            Choose("End conversation");
            float gateDeadline = Time.realtimeSinceStartup + 3f;
            while (portcullis.bounds.min.y < 1.95f && Time.realtimeSinceStartup < gateDeadline) yield return null;
            Assert.That(portcullis.bounds.min.y, Is.GreaterThanOrEqualTo(1.95f));
            yield return ReturnToSpawn();
            yield return WalkNear(inside, "the open castle passage");
            Assert.That(Vector3.Dot(Eye.transform.position - gate, Spawn.forward), Is.GreaterThan(1f));

            yield return TalkTo("knight");
            Choose("Start courtyard training.");
            Assert.That(Status, Does.Contain("0/3").And.Contain("marker 1"));
            Ask(ClaimRequest);
            Assert.That(Status, Does.Contain("0/3").And.Contain("marker 1"));
            Assert.That(FindChoice(ConfirmReward), Is.Null, "The knight cannot offer the reward early.");
            Choose("End conversation");
            AssertWallet(20, 0, "Castle Pass ×1");

            yield return WalkNear(PathOutsideYard, "the outside of the training fence");
            yield return WalkNear(PathIntoYard, "the opening to the training yard");
            for (int number = 1; number <= 3; number++)
            {
                var marker = landmarks.Single(t => t.name == $"Training Marker {number}");
                yield return WalkNear(marker.position, marker.name);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(HudText("Objective/Current objective"), Does.Contain($"{number}/3"));
            }
            AssertWallet(20, 0, "Castle Pass ×1");

            yield return WalkNear(PathIntoYard, "the opening from the training yard");
            yield return WalkNear(PathOutsideYard, "the return path to the knight");
            yield return TalkTo("knight");
            Choose(ConfirmReward);
            Assert.That(Status, Does.Contain("+20 gold").And.Contain("+5 reputation"));
            Assert.That(FindChoice(ConfirmReward), Is.Null);
            Ask(ClaimRequest);
            Assert.That(Panel.LastTurn.Reply, Does.Contain("already been claimed"));
            Assert.That(FindChoice(ConfirmReward), Is.Null);
            Choose("End conversation");
            AssertWallet(40, 5, "Castle Pass ×1");
            Assert.That(HudText("Objective/Current objective"), Does.Contain("Reward collected"));
        }

        private IEnumerator WalkNear(Vector3 goal, string name) =>
            WalkTo(goal, () => Flat(goal - Eye.transform.position).magnitude < .25f, name);

        private IEnumerator TalkTo(string npc)
        {
            yield return Approach(npc);
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.InConversation, Is.True, npc);
            Assert.That(NpcDemoScenario.Roles[Panel.SelectedRole], Is.EqualTo(npc));
        }

        private Button FindChoice(string label) => Questions.GetComponentsInChildren<Button>()
            .SingleOrDefault(button => button.GetComponentInChildren<Text>()?.text == label);

        private void Choose(string label)
        {
            var button = FindChoice(label);
            Assert.That(button, Is.Not.Null, $"Dialogue option: {label}");
            button.onClick.Invoke();
        }

        private void Ask(string message)
        {
            Questions.Find("Free question/Input").GetComponent<InputField>().text = message;
            Questions.Find("Free question/Send").GetComponent<Button>().onClick.Invoke();
            Panel.HandleDesktopFrame(Eye, false, false);
            Assert.That(Panel.LastTurn?.Success, Is.True, message);
        }

        private string HudText(string path) =>
            Panel.transform.Find($"Castle gameplay HUD/{path}").GetComponent<Text>().text;

        private void AssertWallet(int coins, int reputation, string backpack)
        {
            Panel.HandleDesktopFrame(Eye, false, false);
            Assert.That(HudText("Inventory/Wallet"), Does.StartWith($"{coins} coins").And.EndWith($"{reputation} rep"));
            Assert.That(HudText("Inventory/Backpack"), Is.EqualTo(backpack));
        }
    }
}
