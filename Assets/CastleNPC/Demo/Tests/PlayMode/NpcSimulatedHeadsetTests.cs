using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    [Category("SimulatedXR")]
    public sealed class NpcSimulatedHeadsetTests : CastleWalkFixture
    {
        private void Frame(bool a = false, bool b = false, float stick = 0f, bool connected = true)
        {
            Panel.UpdateNearby(Eye.transform);
            Panel.HandleHeadsetFrame(Eye, connected, a, b, stick);
        }

        [UnityTest]
        public IEnumerator LookingAtADistantRiderCannotStartButWalkingUpToTheHorseAndTappingACan()
        {
            // Feed controller samples explicitly, independently of a connected headset or XR loader.
            Panel.enabled = false;
            var knight = Actor("knight");
            var body = knight.GetComponent<CapsuleCollider>();
            var horse = knight.transform.Find("Horse barrel").GetComponent<CapsuleCollider>();
            Eye.transform.LookAt(horse.bounds.center);
            Frame(); Frame(a: true); Frame();
            Assert.That(Panel.InConversation, Is.False);
            Assert.That(Panel.LastTurn, Is.Null);
            yield return Approach("knight");

            // The rider is still outside speaking range. The head, neck or barrel of the horse has
            // already brought its owner inside the radius.
            var chest = Eye.transform.position;
            chest.y = body.bounds.center.y;
            Assert.That(Flat(body.ClosestPoint(chest) - chest).magnitude, Is.GreaterThan(1.6f));
            Eye.transform.rotation = Quaternion.LookRotation(-Flat(knight.transform.position - chest).normalized);
            Frame(a: true);
            Assert.That(Panel.SelectedRole, Is.EqualTo(2), "Nearby horse and rider are one speaker, independently of gaze.");
            Assert.That(Panel.LastTurn, Is.Null);
            for (int i = 0; i < 3; i++) { yield return null; Frame(a: true); }
            Assert.That(Panel.LastTurn, Is.Null, "Holding the first A press must only greet.");
            Frame(); Frame(a: true);
            var turn = Panel.LastTurn;
            Assert.That(turn, Is.Not.Null);
            Assert.That(turn.Success && turn.IsOffline, Is.True);
            Assert.That(turn.Decision.Action, Is.EqualTo(NpcAction.OfferTraining));
            var canvas = Eye.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            var reply = canvas.transform.Find("Reply").GetComponent<Text>();
            Assert.That(reply.text, Does.Contain(turn.Reply.Substring(0, 10)));
            for (int i = 0; i < 3; i++) { yield return null; Frame(a: true); }
            Assert.That(Panel.LastTurn.TurnId, Is.EqualTo(turn.TurnId));
            Frame();
        }

        [UnityTest]
        public IEnumerator BOpensSettingsOnceAndLanguagePersistsWhenResumingAConversation()
        {
            Panel.enabled = false;
            Frame(); Frame(b: true); Frame(b: true);
            Assert.That(Panel.MenuOpen, Is.True);
            Assert.That(Panel.InConversation, Is.False);
            var start = Motor.transform.position;
            for (int i = 0; i < 20; i++) Motor.Step(Vector2.up, 0f, 1f / 60f);
            Assert.That(Flat(Motor.transform.position - start).magnitude, Is.LessThan(.02f));
            Frame(); Frame(b: true); Frame();
            Assert.That(Panel.MenuOpen, Is.False);
            yield return Approach("guard");
            Frame(a: true); Frame();
            Assert.That(Panel.SelectedRole, Is.Zero);
            Frame(b: true); Frame();
            Assert.That(Panel.MenuOpen, Is.True);
            Frame(stick: -1f); Frame();
            Frame(stick: -1f); Frame();
            Frame(a: true); Frame();
            Assert.That(Panel.Chinese, Is.True);
            Assert.That(PlayerPrefs.GetString(LanguageKey), Is.EqualTo("zh"));
            if (Panel.MenuOpen) { Frame(b: true); Frame(); }
            Assert.That(Panel.MenuOpen, Is.False);
            Assert.That(Panel.InConversation, Is.True);
            Assert.That(Panel.SelectedRole, Is.Zero);
            Assert.That(Panel.LastTurn, Is.Null);
            Frame(a: true); Frame();
            Assert.That(Panel.LastTurn, Is.Not.Null);
            Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Assert.That(Panel.LastTurn.Reply, Does.Match("[\u4e00-\u9fff]"));
        }

        [UnityTest]
        public IEnumerator DisconnectOrReconnectWithAHeldButtonCannotSpendATurn()
        {
            Panel.enabled = false;
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("merchant");
            Frame(); Frame(a: true); Frame();
            Assert.That(Panel.SelectedRole, Is.EqualTo(1));
            Frame(connected: false);
            Frame(a: true);
            Frame(a: true);
            Assert.That(provider.Requests, Is.Empty, "A held reconnect must first be released.");
            Frame(); Frame(a: true); Frame(a: true);
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            Frame();
            provider.Replies[0].SetResult("{}");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Panel.LastTurn, Is.Not.Null);
            Assert.That(Panel.LastTurn.Success, Is.False);
            yield return new WaitForSecondsRealtime(.12f);
            Frame();
            var canvas = Eye.GetComponentInChildren<Canvas>(true);
            var options = canvas.transform.Find("Selection").GetComponent<Text>().text;
            Assert.That(options, Does.Contain("Retry").And.Contain("End conversation"));
            Assert.That(options.Split('\n'), Has.Length.EqualTo(2));
            Assert.That(canvas.transform.Find("Reply").GetComponent<Text>().text, Does.Not.Contain("{}"));
            Frame(a: true); Frame(a: true); Frame();
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            Assert.That(provider.Requests[1].PlayerText, Is.EqualTo(provider.Requests[0].PlayerText));
            provider.Replies[1].SetResult("{\"reply\":\"Here is your bread.\",\"action\":\"request_purchase\",\"itemId\":\"bread\",\"quantity\":1,\"questId\":\"\"}");
            deadline = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(NpcAction.RequestPurchase));
        }
    }
}
