using System.Collections;
using System.Linq;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class NpcFarewellTests : CastleWalkFixture
    {
        private static readonly string[] English =
            { "Safe travels, traveler.", "Take care, my friend. Come by again.", "Dismissed. See you on the training field." };
        private static readonly string[] Chinese =
            { "一路平安，旅行者。", "慢走啊，老朋友。下回再来看看。", "解散。训练场见。" };

        private Transform Questions => Panel.transform.Find("NPC questions");
        private Transform Caption => Panel.transform.Find("NPC speech");

        private Button DesktopEndButton()
        {
            var label = Panel.Chinese ? "结束交谈" : "End conversation";
            return Questions.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<Text>().text == label);
        }

        private static void Click(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private AudioSource CheckFarewell(int role, bool chinese)
        {
            var id = NpcDemoScenario.Roles[role];
            var recording = Resources.Load<AudioClip>("CastleNpcFarewells/" + id + (chinese ? "-zh" : "-en"));
            var source = Actor(id).VoiceAnchor.GetComponent<AudioSource>();
            Assert.That(recording, Is.Not.Null, id);
            Assert.That(source, Is.Not.Null, id);
            Assert.That(source.clip, Is.Not.Null, id);
            Assert.That(source.clip.samples, Is.EqualTo(recording.samples), id);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(Panel.InConversation || CastlePlayerMotor.MovementLocked, Is.False);
            Assert.That(Panel.LastTurn, Is.Null);
            Assert.That(Questions.gameObject.activeSelf, Is.False);
            Assert.That(Caption.gameObject.activeSelf, Is.True);
            string expected = (chinese ? Chinese : English)[role];
            Assert.That(Panel.SpokenLine, Is.EqualTo(expected));
            Assert.That(Caption.Find("Reply").GetComponent<Text>().text, Is.EqualTo(expected));
            return source;
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator EndButtonPlaysEveryLocalizedFarewellWhilePlayerWalksAway()
        {
            int greetings = 0;
            Panel.SetSpeechProviderForTesting((id, line, ct) =>
            {
                greetings++;
                return System.Threading.Tasks.Task.FromResult(TestVoice());
            });
            for (int role = 0; role < NpcDemoScenario.Roles.Length; role++)
            {
                string id = NpcDemoScenario.Roles[role];
                yield return Approach(id);
                for (int language = 0; language < 2; language++)
                {
                    bool zh = language == 1;
                    Panel.SetLanguage(zh);
                    Panel.HandleDesktopFrame(Eye, true, false);
                    var end = DesktopEndButton();
                    Click(end);
                    Assert.That(Panel.InConversation || CastlePlayerMotor.MovementLocked, Is.False);
                    yield return WaitForCaption();
                    var source = CheckFarewell(role, zh);
                    Assert.That(greetings, Is.EqualTo(role * 2 + language + 1), "Farewell must use the shipped recording.");
                    // A second pointer event sent to the same row must not restart playback.
                    source.timeSamples = source.clip.samples / 8;
                    Click(end);
                    Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(source.clip.samples / 16));
                    if (role == 0 && !zh)
                    {
                        var jaw = Actor(id).VoiceAnchor;
                        var closed = jaw.localPosition;
                        float opening = 0f;
                        yield return Observe(.52f, () => opening = Mathf.Max(opening, Vector3.Distance(closed, jaw.localPosition)));
                        Assert.That(opening, Is.GreaterThan(.00019f), "Farewell should animate the speaker's jaw.");
                        float deadline = Time.realtimeSinceStartup + source.clip.length / source.pitch + 2f;
                        while (source.clip && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.That(source.clip, Is.Null);
                        Assert.That(Panel.SpokenLine, Is.Empty);
                        Assert.That(Caption.gameObject.activeSelf, Is.False);
                    }
                    else if (!zh)
                    {
                        yield return new WaitForSecondsRealtime(.4f);
                    }
                    if (zh)
                    {
                        var start = Motor.transform.position;
                        // At 5 m/s² the motor needs a third of a second to reach its walking speed.
                        var towardSpawn = Flat(Spawn.position - start).normalized;
                        for (int i = 0; i < 20; i++) StepToward(Spawn.position);
                        Assert.That(Vector3.Dot(Motor.transform.position - start, towardSpawn),
                            Is.GreaterThan(.16f), id + " should move toward spawn while the farewell plays.");
                        Assert.That(source.clip, Is.Not.Null, id + " should stay audible while walking away.");
                    }
                }
                yield return ReturnToSpawn();
            }
        }

        [UnityTest, Category("SimulatedXR")]
        public IEnumerator EndOnControllerReleaseAndPressDoesNotReopenOnHoldOrPlayOnWalkingOut()
        {
            Panel.enabled = false;
            void Frame(bool a = false, bool b = false, float stick = 0f)
                => Panel.HandleHeadsetFrame(Eye, true, a, b, stick);
            for (int role = 0; role < NpcDemoScenario.Roles.Length; role++)
            {
                string id = NpcDemoScenario.Roles[role];
                yield return Approach(id);
                for (int language = 0; language < 2; language++)
                {
                    bool zh = language == 1;
                    Panel.SetLanguage(zh);
                    Frame(); Frame(a: true); Frame();
                    Assert.That(Panel.InConversation, Is.True);
                    Assert.That(Panel.SelectedRole, Is.EqualTo(role));
                    Frame(a: true); Frame();
                    Assert.That(Panel.LastTurn?.Success, Is.True, id + " answers the controller-selected question");
                    Frame(stick: 1f); Frame(); // Wrap up from the first row to End.
                    Frame(b: true); Frame(); Frame(b: true); Frame();
                    Assert.That(Panel.InConversation, Is.True);
                    Frame(a: true);
                    Frame(a: true); Frame(a: true);
                    Assert.That(Panel.InConversation, Is.False);
                    Frame(); Frame(a: true);
                    Assert.That(Panel.InConversation, Is.False, "A quick double tap must not reopen the NPC.");
                    yield return WaitForCaption(() => Frame(a: true));
                    CheckFarewell(role, zh);
                    Frame();
                    yield return new WaitForSecondsRealtime(.4f);
                }
                // A fresh conversation can interrupt a farewell. Forced proximity exits stay silent.
                Frame(a: true); Frame();
                Panel.UpdateNearby(Spawn);
                Assert.That(Panel.InConversation, Is.False);
                Assert.That(Panel.SpokenLine, Is.Empty);
                Assert.That(Actor(id).VoiceAnchor.GetComponent<AudioSource>().clip, Is.Null);
                yield return ReturnToSpawn();
            }
        }

        [UnityTest, Category("Desktop")]
        public IEnumerator EndingDuringARequestRejectsLateDialogueAndSwitchingSpeakersStopsFarewell()
        {
            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            yield return Approach("guard");
            Panel.HandleDesktopFrame(Eye, true, false);
            Panel.Submit("May I enter?");
            Panel.HandleDesktopFrame(Eye, false, false);
            Click(DesktopEndButton());
            provider.Replies.Single().SetResult("{\"reply\":\"Let me check.\",\"action\":\"check_pass\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
            yield return WaitForCaption();
            var guardVoice = CheckFarewell(0, false);
            Assert.That(Panel.LastTurn, Is.Null);
            Assert.That(Panel.SpokenLine, Is.EqualTo(English[0]));
            // The market is farther away than one farewell's walking time. Put the second
            // speaker on the open approach for this interruption race in the additive test scene.
            var feet = Motor.transform.position;
            Actor("merchant").transform.position = feet + Flat(Spawn.position - feet).normalized * 1.15f;
            Physics.SyncTransforms();
            Assert.That(guardVoice.isPlaying, Is.True, "The second greeting interrupts a live farewell.");
            Begin("merchant");
            Assert.That(guardVoice.clip, Is.Null);
            yield return WaitForCaption();
            Assert.That(Panel.SpokenLine, Is.Not.Empty.And.Not.EqualTo(English[0]));
            Assert.That(provider.Requests.Count, Is.EqualTo(1));
        }
    }
}
