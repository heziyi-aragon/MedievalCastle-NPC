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
                voice.pitch = actor.RoleId == "guard" ? .94f : actor.RoleId == "merchant" ? 1.05f : 1f;
                voice.clip = clip;
                if (!greeting) StartDecisionGesture(actor, action);
                voice.Play();
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
                speaker.Listen(viewer);
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
