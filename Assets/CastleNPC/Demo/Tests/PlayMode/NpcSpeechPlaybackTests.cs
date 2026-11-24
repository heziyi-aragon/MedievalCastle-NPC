using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class NpcSpeechPlaybackTests : CastleWalkFixture
    {
        [UnityTest]
        public IEnumerator AllThreeRecordedWelcomesPlayFromTheCharactersMouths()
        {
            var ids = new[] { "guard", "merchant", "knight" };
            var expectedPitches = new[] { .94f, 1.05f, 1f };
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
                Assert.That(source.spatialBlend, Is.EqualTo(1));
                Vector3 rest = jaw.localPosition;
                float travel = 0;
                yield return Observe(.95f, () => travel = Mathf.Max(travel, Vector3.Distance(rest, jaw.localPosition)));
                Assert.That(travel, Is.GreaterThan(.00019f), ids[i] + " recorded voice opens jaw");
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
            Assert.That(Panel.SpokenLine, Is.EqualTo("你好，旅行者。"));
            Assert.That(calls, Is.EqualTo(2));
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
            var actor = Actor("guard");
            Panel.Submit(NpcDemoScenario.Prompt(0, 0, false));
            var turn = Panel.LastTurn;
            Assert.That(turn.Decision.Action, Is.EqualTo(NpcAction.CheckPass));
            Assert.That(lines[1], Is.EqualTo(turn.Reply));
            Assert.That(Panel.SpokenLine, Is.EqualTo(turn.Reply));
            Assert.That(tokens[0].IsCancellationRequested, Is.True);
            requests[0].SetResult(TestVoice());
            yield return null;
            Assert.That(actor.VoiceAnchor.GetComponent<AudioSource>(), Is.Null);
            Panel.ToggleMenu(); Panel.ToggleMenu();
            Assert.That(requests.Count, Is.EqualTo(2));
            requests[1].SetException(new FormatException("Test damaged audio"));
            yield return null;
            Assert.That(Panel.LastTurn.TurnId, Is.EqualTo(turn.TurnId));
            Assert.That(Panel.SpokenLine, Is.EqualTo(turn.Reply));
            Panel.Submit(NpcDemoScenario.Prompt(0, 1, false));
            Assert.That(requests.Count, Is.EqualTo(3));
            Panel.EndTalk();
            Assert.That(tokens[2].IsCancellationRequested, Is.True);
            requests[2].SetResult(TestVoice());
            yield return null;
            Assert.That(Panel.SpokenLine, Is.Empty);
            Assert.That(actor.VoiceAnchor.GetComponent<AudioSource>(), Is.Null);
        }
    }
}
