using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class NpcSpeechPlaybackTests : CastleWalkFixture
    {
        [UnityTest]
        public IEnumerator AllThreeRecordedWelcomesPlayFromTheCharactersMouths()
        {
            var ids = new[] { "guard", "merchant", "knight" };
            var expectedPitches = new[] { .94f, .90f, 1.045f };
            Panel.SetSpeechProviderForTesting(null);
            for (int i = 0; i < ids.Length; i++)
            {
                bool zh = i == 0;
                var resource = Resources.Load<AudioClip>("CastleNpcGreetings/" + ids[i] + (zh ? "-zh" : "-en"));
                // Verify the recording exists before entering the production cache path.
                Assert.That(resource, Is.Not.Null);
                Panel.SetLanguage(zh);
                yield return Approach(ids[i]);
                Service.Mode = NpcProviderMode.DeepSeek;
                Begin(ids[i]);
                Service.Mode = NpcProviderMode.Offline;
                var jaw = Actor(ids[i]).VoiceAnchor;
                var source = jaw.GetComponent<AudioSource>();
                Assert.That(source, Is.Not.Null, ids[i]);
                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.clip.samples, Is.EqualTo(resource.samples));
                Assert.That(source.pitch, Is.EqualTo(expectedPitches[i]).Within(.001f));
                if (ids[i] == "merchant")
                    Assert.That(source.GetComponent<AudioLowPassFilter>().cutoffFrequency, Is.EqualTo(6000f).Within(1f));
                if (ids[i] == "knight")
                    Assert.That(source.GetComponent<AudioHighPassFilter>().cutoffFrequency, Is.EqualTo(150f).Within(1f));
                Assert.That(source.spatialBlend, Is.EqualTo(1));
                Vector3 rest = jaw.localPosition;
                Assert.That(Panel.SpokenLine, Is.Empty, ids[i] + " recording starts with a quiet lead-in.");
                float travel = 0;
                yield return Observe(.95f, () => travel = Mathf.Max(travel, Vector3.Distance(rest, jaw.localPosition)));
                Assert.That(travel, Is.GreaterThan(.00019f), ids[i] + " recorded voice opens jaw");
                Assert.That(Panel.SpokenLine, Is.Not.Empty);
                Panel.EndTalk();
                Assert.That(jaw.localPosition, Is.EqualTo(rest));
                Assert.That(source.clip, Is.Null);
                yield return ReturnToSpawn();
            }
        }

        [UnityTest]
        public IEnumerator GreetingIsSpatialAndCachedByRoleAndLanguage()
        {
            int calls = 0;
            Panel.SetSpeechProviderForTesting((role, line, ct) => { calls++; return Task.FromResult(TestVoice()); });
            yield return Approach("guard");
            Begin("guard");
            var actor = Actor("guard");
            var source = actor.VoiceAnchor.GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            yield return WaitForCaption();
            Assert.That(Panel.SpokenLine, Is.EqualTo("Hello, traveler."));
            Assert.That(Panel.LastTurn, Is.Null);
            var first = source.clip;
            Panel.ToggleMenu();
            Panel.ToggleMenu();
            Assert.That(source.clip, Is.SameAs(first));
            Panel.EndTalk();
            Assert.That(source.clip, Is.Null);
            Begin("guard");
            Assert.That(source.clip, Is.SameAs(first));
            Assert.That(calls, Is.EqualTo(1));
            Panel.EndTalk();
            Panel.SetLanguage(true);
            Begin("guard");
            yield return WaitForCaption();
            Assert.That(Panel.SpokenLine, Is.EqualTo("你好，旅行者。"));
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest, Category("SimulatedXR")]
        public IEnumerator ReplyCaptionAndPagingStartAtFirstAudibleFrameAndRemainAfterPlayback()
        {
            Panel.enabled = false;
            void Frame(bool a = false)
            {
                Panel.UpdateVoice();
                Panel.HandleHeadsetFrame(Eye, true, a, false, 0);
            }

            var provider = new ConversationSelectionTests.ControlledProvider();
            Service.SetProviderForTesting(provider);
            var delayedVoice = new TaskCompletionSource<AudioClip>();
            int lines = 0;
            Panel.SetSpeechProviderForTesting((id, line, token) => ++lines == 1 ?
                Task.FromResult(TestVoice()) : delayedVoice.Task);
            yield return Approach("merchant");
            Frame(); Frame(true); Frame(); Frame(true); Frame();
            Assert.That(provider.Requests.Count, Is.EqualTo(1));
            const string reply = "The lantern costs five copper coins. I set one on the counter at dusk so travelers can see the wares before they choose.";
            provider.Replies[0].SetResult("{\"reply\":\"" + reply + "\",\"action\":\"none\",\"itemId\":\"\",\"quantity\":0,\"questId\":\"\"}");
            yield return new WaitForSecondsRealtime(.15f);
            Frame();
            Assert.That(Panel.LastTurn?.Success, Is.True);
            var caption = Panel.transform.Find("NPC speech");
            var status = Panel.transform.Find("NPC questions/Status");
            var choice = Panel.transform.Find("NPC questions/Option 1/Label").GetComponent<Text>();
            Assert.That(caption.gameObject.activeSelf || status.gameObject.activeSelf, Is.False);
            Assert.That(choice.text, Is.EqualTo(NpcDemoScenario.Prompt(1, 0, false)), "A hidden reply cannot offer a Next page action.");

            const int rate = 24000, onset = rate / 4;
            var samples = new float[rate];
            for (int i = onset; i < samples.Length; i++)
                samples[i] = .2f * Mathf.Cos((i - onset) * 2f * Mathf.PI * 400f / rate);
            var recording = AudioClip.Create("250 ms silence followed by speech marker", samples.Length, 1, rate, false);
            Assert.That(recording.SetData(samples, 0), Is.True);
            delayedVoice.SetResult(recording);

            var jaw = Actor("merchant").VoiceAnchor;
            var closed = jaw.localPosition;
            var source = jaw.GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null);
            int firstAudioFrame = -1, firstCaptionFrame = -1;
            float mouthTravel = 0;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Frame();
                if (source.clip == recording && source.timeSamples >= onset && firstAudioFrame < 0)
                    firstAudioFrame = Time.frameCount;
                if (caption.gameObject.activeSelf && firstCaptionFrame < 0)
                    firstCaptionFrame = Time.frameCount;
                if (firstAudioFrame < 0)
                    Assert.That(Vector3.Distance(jaw.localPosition, closed), Is.LessThan(.0001f), "Jaw opened before speech.");
                else
                    mouthTravel = Mathf.Max(mouthTravel, Vector3.Distance(jaw.localPosition, closed));
                if (source.clip == null && firstAudioFrame >= 0) break;
            }
            Assert.That(firstAudioFrame, Is.GreaterThanOrEqualTo(0), "The source must play through the generated marker.");
            TestContext.WriteLine($"250 ms marker on audio frame {firstAudioFrame}, subtitle on frame {firstCaptionFrame}.");
            Assert.That(firstCaptionFrame - firstAudioFrame, Is.InRange(0, 1),
                $"First audible frame {firstAudioFrame}, first visible subtitle {firstCaptionFrame}.");
            Assert.That(mouthTravel, Is.GreaterThan(.00019f));
            Assert.That(source.clip, Is.Null, "Complete the clip before checking reading controls.");
            Assert.That(Panel.SpokenLine, Is.EqualTo(reply));
            Assert.That(caption.gameObject.activeSelf, Is.True);
            Assert.That(choice.text, Is.EqualTo("Next page"));
            Assert.That(lines, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ADelayedVoiceCannotInterruptANewTurnAndFailureLeavesTheDecision()
        {
            var requests = new List<TaskCompletionSource<AudioClip>>();
            var tokens = new List<CancellationToken>();
            var lines = new List<string>();
            Panel.SetSpeechProviderForTesting((id, text, token) =>
            {
                tokens.Add(token);
                lines.Add(text);
                var op = new TaskCompletionSource<AudioClip>();
                requests.Add(op);
                return op.Task;
            });
            yield return Approach("guard");
            Begin("guard");
            Panel.HandleDesktopFrame(Eye, false, false);
            var caption = Panel.transform.Find("NPC speech").gameObject;
            var status = Panel.transform.Find("NPC questions/Status").GetComponent<Text>();
            Assert.That(Panel.SpokenLine, Is.Empty);
            Assert.That(caption.activeSelf || status.gameObject.activeSelf, Is.False);
            var actor = Actor("guard");
            Panel.Submit(NpcDemoScenario.Prompt(0, 0, false));
            var turn = Panel.LastTurn;
            Assert.That(turn.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Assert.That(lines[1], Is.EqualTo(turn.Reply));
            Assert.That(Panel.SpokenLine, Is.Empty);
            Assert.That(tokens[0].IsCancellationRequested, Is.True);
            requests[0].SetResult(TestVoice());
            yield return null;
            Panel.HandleDesktopFrame(Eye, false, false);
            Assert.That(caption.activeSelf || status.gameObject.activeSelf, Is.False);
            Assert.That(actor.VoiceAnchor.GetComponent<AudioSource>(), Is.Null);
            Panel.ToggleMenu(); Panel.ToggleMenu();
            Assert.That(requests.Count, Is.EqualTo(2));
            requests[1].SetException(new TimeoutException("Simulated speech timeout"));
            yield return null;
            Panel.HandleDesktopFrame(Eye, false, false);
            Assert.That(Panel.LastTurn.TurnId, Is.EqualTo(turn.TurnId));
            Assert.That(Panel.LastTurn.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Assert.That(Panel.SpokenLine, Is.EqualTo(turn.Reply));
            Assert.That(caption.activeSelf && status.gameObject.activeSelf, Is.True);
            Assert.That(status.text, Is.EqualTo("Voice temporarily unavailable."));
            var firstOption = Panel.transform.Find("NPC questions/Option 1/Label").GetComponent<Text>();
            Assert.That(firstOption.text, Is.EqualTo(NpcDemoScenario.Prompt(0, 0, false)));
            Panel.Retry();
            Assert.That(requests.Count, Is.EqualTo(2), "Speech failure must not retry the dialogue turn.");
            Panel.SetLanguage(true);
            Panel.HandleDesktopFrame(Eye, false, false);
            Assert.That(status.text, Is.EqualTo("语音暂不可用"));
            Panel.Submit(NpcDemoScenario.Prompt(0, 1, false));
            Assert.That(requests.Count, Is.EqualTo(3));
            Panel.EndTalk();
            yield return Approach("merchant");
            Begin("merchant");
            Assert.That(requests.Count, Is.EqualTo(4));
            Assert.That(tokens[2].IsCancellationRequested, Is.True);
            requests[2].SetResult(TestVoice());
            yield return null;
            Assert.That(Panel.SpokenLine, Is.Empty);
            Assert.That(actor.VoiceAnchor.GetComponent<AudioSource>(), Is.Null);
            requests[3].SetResult(TestVoice());
            yield return WaitForCaption();
            Assert.That(Panel.SpokenLine, Is.EqualTo(lines[3]));
            Assert.That(Panel.SelectedRole, Is.EqualTo(1));
        }
    }
}
