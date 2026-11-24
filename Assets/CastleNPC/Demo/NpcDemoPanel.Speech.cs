using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MedievalCastle.NPC;
using UnityEngine;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private readonly Dictionary<(string role, bool chinese), AudioClip> greetings =
            new Dictionary<(string, bool), AudioClip>();
        private readonly float[] mouthWindow = new float[256];
        private CancellationTokenSource voiceRequest;
        private CastleNpcCharacter speaker;
        private AudioSource voice;
        private AudioClip temporaryReply;
        private Func<string, string, CancellationToken, Task<AudioClip>> speechOverride;
        private float silentGestureUntil;
        private bool farewellActive;
        private float farewellStartedAt;

        internal string SpokenLine { get; private set; } = "";
        internal void SetSpeechProviderForTesting(Func<string, string, CancellationToken, Task<AudioClip>> provider)
            => speechOverride = provider;

        private static string Welcome(string id, bool zh)
        {
            switch (id)
            {
                case "guard": return zh ? "你好，旅行者。" : "Hello, traveler.";
                case "merchant": return zh ? "欢迎光临，旅行者。想看看什么？" : "Welcome, traveler. What catches your eye?";
                default: return zh ? "你好，旅行者。是来参加训练的吗？" : "Greetings, traveler. Are you here to train?";
            }
        }

        private void StartGreeting(CastleNpcCharacter actor)
        {
            SpokenLine = Welcome(actor.RoleId, chinese);
            PlayLine(actor, SpokenLine, chinese, true, NpcAction.None);
        }

        private void StartReply(CastleNpcCharacter actor, NpcTurnResult reply)
        {
            SpokenLine = reply.Reply;
            PlayLine(actor, reply.Reply, replyIsChinese, false, reply.Decision.Action);
        }

        internal void EndTalkWithFarewell()
        {
            if (!InConversation || MenuOpen) return;
            var actor = actors[role];
            var listener = viewer;
            EndTalk();
            if (!actor || !actor.isActiveAndEnabled) return;

            switch (actor.RoleId)
            {
                case "guard": SpokenLine = T("Safe travels, traveler.", "一路平安，旅行者。"); break;
                case "merchant": SpokenLine = T("Take care, my friend. Come by again.", "慢走啊，老朋友。下回再来看看。"); break;
                default: SpokenLine = T("Chin up! See you on the training field!", "昂首向前！训练场见！"); break;
            }
            farewellActive = true;
            farewellStartedAt = Time.unscaledTime;
            speaker = actor;
            actor.Speak(NpcAction.None, listener);
            actor.SetMouthOpening(0f);
            var clip = Resources.Load<AudioClip>("CastleNpcFarewells/" + actor.RoleId + (chinese ? "-zh" : "-en"));
            if (clip) PlayClip(actor, clip);
            else silentGestureUntil = Time.unscaledTime + 2.5f;
            ShowFarewellCaption(listener ? listener.GetComponent<Camera>() : null);
        }

        private async void PlayLine(CastleNpcCharacter actor, string line, bool zh, bool greeting, NpcAction action)
        {
            StopVoice();
            speaker = actor;
            actor.SetMouthOpening(0f);
            var request = new CancellationTokenSource();
            voiceRequest = request;
            var cacheKey = (actor.RoleId, zh);
            AudioClip clip = null;
            try
            {
                if (!greeting || !greetings.TryGetValue(cacheKey, out clip))
                {
                    if (speechOverride != null)
                        clip = await speechOverride(actor.RoleId, line, request.Token);
                    else if (service.UsesDeepSeek)
                    {
                        // Three recorded welcome lines ship with the scene for immediate playback.
                        var saved = greeting ? Resources.Load<AudioClip>(
                            "CastleNpcGreetings/" + actor.RoleId + (zh ? "-zh" : "-en")) : null;
                        clip = saved ? Instantiate(saved) : await DeepSeekNpcSpeech.GenerateAsync(actor.RoleId, line, request.Token);
                    }
                    if (voiceRequest != request)
                    {
                        if (clip) Destroy(clip);
                        return;
                    }
                    if (clip && greeting) greetings[cacheKey] = clip;
                    else temporaryReply = clip;
                }
                if (!clip)
                {
                    if (!greeting) StartSilentGesture(actor, action);
                    return;
                }
                if (!greeting) StartDecisionGesture(actor, action);
                PlayClip(actor, clip);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (voiceRequest == request)
                {
                    // Exclude provider response bodies, account details and credentials from the log.
                    Debug.LogWarning("NPC voice unavailable (" + ex.GetType().Name + ").");
                    if (!greeting) StartSilentGesture(actor, action);
                }
            }
            finally
            {
                if (voiceRequest == request) voiceRequest = null;
                request.Dispose();
            }
        }

        private void PlayClip(CastleNpcCharacter actor, AudioClip clip)
        {
            voice = actor.VoiceAnchor.GetComponent<AudioSource>();
            if (!voice)
            {
                voice = actor.VoiceAnchor.gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.dopplerLevel = 0f;
                voice.minDistance = 1.25f;
                voice.maxDistance = 12f;
            }
            NpcVoiceProfile.Apply(voice, actor.RoleId);
            voice.clip = clip;
            voice.Play();
        }

        private void StartDecisionGesture(CastleNpcCharacter actor, NpcAction action)
        {
            Vector3? target = trainingTarget ? (Vector3?)(trainingTarget.position + Vector3.up) : null;
            actor.Speak(action, viewer, target);
        }

        private void StartSilentGesture(CastleNpcCharacter actor, NpcAction action)
        {
            StartDecisionGesture(actor, action);
            silentGestureUntil = Time.unscaledTime + 1.35f;
        }

        private void UpdateVoice()
        {
            if (farewellActive && (!speaker || !speaker.isActiveAndEnabled))
            {
                StopVoice();
                return;
            }
            if (!speaker) return;
            if (voice && voice.isPlaying && voice.clip)
            {
                var clip = voice.clip;
                if (clip.GetData(mouthWindow, Mathf.Clamp(voice.timeSamples, 0, Mathf.Max(0, clip.samples - mouthWindow.Length))))
                {
                    float sum = 0;
                    foreach (float sample in mouthWindow) sum += sample * sample;
                    speaker.SetMouthOpening(Mathf.Clamp01(Mathf.Sqrt(sum / mouthWindow.Length) * 6f));
                }
            }
            else if (voice || (silentGestureUntil > 0 && Time.unscaledTime >= silentGestureUntil))
            {
                if (!farewellActive) speaker.Listen(viewer);
                StopVoice();
            }
        }

        private void StopVoice()
        {
            var pending = voiceRequest;
            voiceRequest = null;
            pending?.Cancel();
            if (voice)
            {
                voice.Stop();
                voice.clip = null;
            }
            if (speaker) speaker.SetMouthOpening(0f);
            if (farewellActive)
            {
                if (speaker) speaker.EndConversation();
                farewellActive = false;
                SpokenLine = "";
                if (speech) speech.gameObject.SetActive(false);
            }
            if (temporaryReply) Destroy(temporaryReply);
            temporaryReply = null;
            voice = null;
            speaker = null;
            silentGestureUntil = 0;
        }

        private void ReleaseGreetings()
        {
            foreach (var clip in greetings.Values)
                if (clip) Destroy(clip);
            greetings.Clear();
        }
    }
}
