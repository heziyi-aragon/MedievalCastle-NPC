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
            Panel.SetLanguage(true);
            var knight = Actor("knight");
            var body = knight.GetComponent<CapsuleCollider>();
            var horse = knight.transform.Find("Horse barrel").GetComponent<CapsuleCollider>();
            Eye.transform.LookAt(horse.bounds.center);
            Frame(); Frame(a: true); Frame();
            var hint = Eye.transform.Find("NPC exploration hint");
            Assert.That(hint.gameObject.activeSelf, Is.False, "The spawn view must also hide the hint background.");
            Assert.That(Panel.InConversation, Is.False);
            Assert.That(Panel.LastTurn, Is.Null);
            yield return Approach("knight");
            yield return new WaitForSecondsRealtime(.12f);
            Frame();
            Assert.That(hint.gameObject.activeSelf, Is.True);
            Assert.That(hint.Find("Hint").GetComponent<Text>().text, Is.EqualTo("A · 与骑士交谈"));

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
            yield return WaitForCaption(() => Frame(a: true));
            var canvas = Panel.transform.Find("NPC speech").GetComponent<Canvas>();
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
            var chat = Panel.transform.Find("NPC questions");
            Assert.That(chat.Find("Controls").GetComponent<Text>().text, Does.Not.Contain("B"));
            Frame(b: true); Frame();
            Assert.That(Panel.MenuOpen, Is.True);
            Assert.That(chat.gameObject.activeSelf, Is.False);
            var sheet = Eye.transform.Find("Castle language settings/Parchment");
            Assert.That(sheet.GetComponent<RawImage>().texture, Is.Not.Null);
            Assert.That(sheet.Find("Close menu hint").GetComponent<Text>().text, Is.EqualTo("Press B again to close"));
            var activeLabels = sheet.GetComponentsInChildren<Text>();
            Assert.That(System.Array.Exists(activeLabels, t => t.text.Contains("Resume")), Is.False);
            Assert.That(sheet.Find("English").GetComponent<Text>().text, Is.EqualTo("English"));
            Assert.That(sheet.Find("Chinese").GetComponent<Text>().text, Is.EqualTo("中文"));
            Frame(stick: -1f); Frame(stick: -1f); Frame();
            Assert.That(Panel.Chinese, Is.False, "Moving the focus must not change the saved language.");
            Frame(a: true); Frame(a: true); Frame();
            Assert.That(Panel.MenuOpen, Is.True, "Selecting a language leaves settings open.");
            Assert.That(Panel.Chinese, Is.True);
            Assert.That(PlayerPrefs.GetString(LanguageKey), Is.EqualTo("zh"));
            Frame(b: true); Frame(b: true); Frame();
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
        public IEnumerator AnInFlightReplySurvivesSettingsAndClosingWithAHeldADoesNotSendAgain()
        {
            Panel.enabled = false;
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Frame(); Frame(a: true); Frame(); Frame(a: true); Frame();
            Assert.That(Panel.LastTurn, Is.Null);
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            Frame(b: true); Frame(b: true); Frame();
            Frame(stick: -1f); Frame(); Frame(a: true); Frame();
            Assert.That(Panel.MenuOpen && Panel.Chinese, Is.True);
            Assert.That(Panel.LastTurn, Is.Null);
            string answer = "I can check your pass. " + new string('x', 212);
            provider.Replies[0].SetResult("{\"reply\":\"" + answer + "\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Panel.LastTurn, Is.Not.Null);
            Assert.That(Panel.LastTurn.Success, Is.True);
            var turnId = Panel.LastTurn.TurnId;
            yield return WaitForCaption(() => Frame());
            Frame(a: true, b: true); Frame(a: true); Frame();
            Assert.That(Panel.MenuOpen, Is.False);
            Assert.That(Panel.SelectedRole, Is.Zero);
            Assert.That(Panel.LastTurn.TurnId, Is.EqualTo(turnId));
            var reply = Panel.transform.Find("NPC speech/Reply").GetComponent<Text>();
            var page = Panel.transform.Find("NPC speech/Page").GetComponent<Text>();
            Assert.That(page.text, Is.EqualTo("1 / 3"));
            Assert.That(reply.text, Does.Contain("check your pass"));
            Frame(a: true); Frame();
            var secondPage = reply.text;
            Assert.That(page.text, Is.EqualTo("2 / 3"));
            Frame(b: true); Frame(); Frame(stick: 1f); Frame(); Frame(a: true); Frame();
            Frame(b: true); Frame();
            Assert.That(Panel.Chinese, Is.False);
            Assert.That(reply.text, Is.EqualTo(secondPage), "Changing UI language must preserve the page of an existing reply.");
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
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
            var canvas = Panel.transform.Find("NPC speech").GetComponent<Canvas>();
            var card = Panel.transform.Find("NPC questions");
            Assert.That(card.Find("Option 1/Label").GetComponent<Text>().text, Is.EqualTo("Retry"));
            Assert.That(card.Find("Option 2/Label").GetComponent<Text>().text, Is.EqualTo("End conversation"));
            Assert.That(card.Find("Option 3").gameObject.activeSelf, Is.False);
            Assert.That(card.Find("Status").GetComponent<Text>().text, Does.Contain("Unable"));
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Frame(a: true); Frame(a: true);
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            Assert.That(provider.Requests[1].PlayerText, Is.EqualTo(provider.Requests[0].PlayerText));
            provider.Replies[1].SetResult("{\"reply\":\"One Castle Pass costs ten coins.\",\"action\":\"request_purchase\",\"itemId\":\"castle_pass\",\"quantity\":1,\"questId\":\"\"}");
            deadline = Time.realtimeSinceStartup + 3f;
            while (Panel.LastTurn == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(NpcAction.RequestPurchase));
            yield return new WaitForSecondsRealtime(.12f);
            Frame(a: true);
            Assert.That(card.Find("Option 1/Label").GetComponent<Text>().text, Is.EqualTo("Confirm purchase"));
            Assert.That(card.Find("Status").GetComponent<Text>().text, Does.Contain("Total 10 coins"));
            Frame(); Frame(a: true); Frame(a: true);
            Assert.That(card.Find("Status").GetComponent<Text>().text, Does.Contain("Balance: 20 coins"));
            Assert.That(provider.Requests, Has.Count.EqualTo(2), "Confirmation is handled by Unity.");
        }
    }
}
