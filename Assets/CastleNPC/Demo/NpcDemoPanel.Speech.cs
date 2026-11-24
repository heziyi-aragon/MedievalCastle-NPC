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
        private enum VoiceLineKind { Greeting, Reply, Farewell }

        private readonly Dictionary<(string role, bool chinese), AudioClip> greetings =
            new Dictionary<(string, bool), AudioClip>();
        private readonly float[] mouthWindow = new float[256];
        private CancellationTokenSource voiceRequest;
        private CastleNpcCharacter speaker;
        private AudioSource voice;
        private AudioClip temporaryReply;
        private Func<string, string, CancellationToken, Task<AudioClip>> speechOverride;
        private VoiceLineKind lineKind;
        private NpcAction lineAction;
        private string pendingCaption;
        private Transform voiceListener;
        private int onsetSample;
        private float playbackRequestedAt;
        private bool playbackAdvanced;
        private float silentGestureUntil;
        private bool farewellActive => lineKind == VoiceLineKind.Farewell;
        private float farewellStartedAt;

        internal string SpokenLine { get; private set; } = "";
        internal bool VoiceUnavailable { get; private set; }
        internal void SetSpeechProviderForTesting(Func<string, string, CancellationToken, Task<AudioClip>> provider)
            => speechOverride = provider;

        private static string Welcome(string id, bool zh)
        {
            switch (id)
            {
                case "guard": return zh ? "你好，旅行者。" : "Hello, traveler.";
                case "merchant": return zh ? "欢迎光临，旅行者。想看看什么？" : "Welcome, traveler. What catches your eye?";
                default: return zh ? "抬头，新兵。准备好操练了吗？" : "Eyes front, recruit. Ready for the drill?";
            }
        }

        private void StartGreeting(CastleNpcCharacter actor)
        {
            PlayLine(actor, Welcome(actor.RoleId, chinese), chinese, VoiceLineKind.Greeting, NpcAction.None, viewer);
        }

        private void StartReply(CastleNpcCharacter actor, NpcTurnResult reply)
        {
            PlayLine(actor, reply.Reply, replyIsChinese, VoiceLineKind.Reply, reply.Decision.Action, viewer);
        }

        internal void EndTalkWithFarewell()
        {
            if (!InConversation || MenuOpen) return;
            var actor = actors[role];
            var listener = viewer;
            EndTalk();
            if (!actor || !actor.isActiveAndEnabled) return;

            string farewell;
            switch (actor.RoleId)
            {
                case "guard": farewell = T("Safe travels, traveler.", "一路平安，旅行者。"); break;
                case "merchant": farewell = T("Take care, my friend. Come by again.", "慢走啊，老朋友。下回再来看看。"); break;
                default: farewell = T("Dismissed. See you on the training field.", "解散。训练场见。"); break;
            }
            farewellStartedAt = Time.unscaledTime;
            actor.Listen(listener);
            PlayLine(actor, farewell, chinese, VoiceLineKind.Farewell, NpcAction.None, listener);
        }

        private async void PlayLine(CastleNpcCharacter actor, string line, bool zh, VoiceLineKind kind,
            NpcAction action, Transform listener)
        {
            StopVoice();
            speaker = actor;
            voiceListener = listener;
            pendingCaption = line;
            lineKind = kind;
            lineAction = action;
            actor.SetMouthOpening(0f);
            var request = new CancellationTokenSource();
            voiceRequest = request;
            var cacheKey = (actor.RoleId, zh);
            AudioClip clip = null;
            try
            {
                bool greeting = kind == VoiceLineKind.Greeting;
                bool cacheGreeting = false;
                if (kind == VoiceLineKind.Farewell)
                    clip = Resources.Load<AudioClip>("CastleNpcFarewells/" + actor.RoleId + (zh ? "-zh" : "-en"));
                else if (!greeting || !greetings.TryGetValue(cacheKey, out clip))
                {
                    if (speechOverride != null)
                        clip = await speechOverride(actor.RoleId, line, request.Token);
                    else if (service.UsesDeepSeek)
                    {
                        // Use a shipped welcome when available for this role and language.
                        var saved = greeting ? Resources.Load<AudioClip>(
                            "CastleNpcGreetings/" + actor.RoleId + (zh ? "-zh" : "-en")) : null;
                        clip = saved ? Instantiate(saved) : await DeepSeekNpcSpeech.GenerateAsync(actor.RoleId, line, request.Token);
                    }
                    if (voiceRequest != request || !actor || !actor.isActiveAndEnabled)
                    {
                        if (clip) Destroy(clip);
                        if (voiceRequest == request) StopVoice();
                        return;
                    }
                    temporaryReply = clip;
                    cacheGreeting = greeting;
                }
                if (clip) onsetSample = FindVoiceOnset(clip);
                if (clip && onsetSample >= 0)
                {
                    if (cacheGreeting)
                    {
                        greetings[cacheKey] = clip;
                        temporaryReply = null;
                    }
                    PlayClip(actor, clip);
                }
                else ShowTextOnly();
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (voiceRequest == request)
                {
                    // Exclude provider response bodies, account details and credentials from the log.
                    Debug.LogWarning("NPC voice unavailable (" + ex.GetType().Name + ").");
                    ShowTextOnly();
                }
            }
            finally
            {
                if (voiceRequest == request) voiceRequest = null;
                request.Dispose();
            }
        }

        // Inspect 5 ms PCM windows in blocks. The cursor stays in clip sample frames,
        // so the same onset works at each role's pitch and at any rendering frame rate.
        private static int FindVoiceOnset(AudioClip clip)
        {
            const float minimumMeanSquare = .00001f; // -50 dBFS RMS
            int window = Mathf.Max(1, clip.frequency / 200);
            int blockFrames = Mathf.Min(window * 32, clip.samples);
            if (blockFrames == 0) return -1;
            var pcm = new float[blockFrames * clip.channels];
            for (int block = 0; block < clip.samples; block += blockFrames)
            {
                if (!clip.GetData(pcm, block)) return -1;
                int count = Mathf.Min(blockFrames, clip.samples - block);
                for (int frame = 0; frame < count; frame += window)
                {
                    int start = frame * clip.channels;
                    int end = Mathf.Min(frame + window, count) * clip.channels;
                    float sum = 0f;
                    for (int i = start; i < end; i++) sum += pcm[i] * pcm[i];
                    if (sum >= minimumMeanSquare * (end - start))
                    {
                        for (int i = start; i < end; i++)
                            if (pcm[i] * pcm[i] >= minimumMeanSquare) return block + i / clip.channels;
                    }
                }
            }
            return -1;
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
            playbackRequestedAt = Time.unscaledTime;
            playbackAdvanced = false;
            voice.Play();
            UpdateVoice();
        }

        private void PublishCaption()
        {
            if (pendingCaption == null) return;
            SpokenLine = pendingCaption;
            pendingCaption = null;
            nextHudDraw = 0;
            if (lineKind != VoiceLineKind.Greeting)
            {
                Vector3? target = trainingTarget ? (Vector3?)(trainingTarget.position + Vector3.up) : null;
                speaker.Speak(lineAction, voiceListener, target);
            }
        }

        private void ShowTextOnly()
        {
            if (voice) { voice.Stop(); voice.clip = null; }
            voice = null;
            VoiceUnavailable = true;
            nextHudDraw = 0;
            PublishCaption();
            silentGestureUntil = Time.unscaledTime + (farewellActive ? 2.5f : 1.35f);
        }

        internal void UpdateVoice()
        {
            if (farewellActive && (!speaker || !speaker.isActiveAndEnabled))
            {
                StopVoice();
                return;
            }
            if (!speaker) return;
            if (voice)
            {
                if (AudioListener.pause) return;
                if (voice.clip && voice.isPlaying)
                {
                    var clip = voice.clip;
                    int cursor = voice.timeSamples;
                    if (cursor > 0) playbackAdvanced = true;
                    // Play() can set isPlaying before the first audio buffer advances.
                    if (playbackAdvanced && cursor >= onsetSample)
                    {
                        PublishCaption();
                        if (clip.GetData(mouthWindow, Mathf.Clamp(cursor, 0, Mathf.Max(0, clip.samples - mouthWindow.Length))))
                        {
                            float sum = 0;
                            foreach (float sample in mouthWindow) sum += sample * sample;
                            speaker.SetMouthOpening(Mathf.Clamp01(Mathf.Sqrt(sum / mouthWindow.Length) * 6f));
                        }
                    }
                    if (playbackAdvanced) return;
                }
                if (!playbackAdvanced)
                {
                    if (Time.unscaledTime - playbackRequestedAt > 1.5f) ShowTextOnly();
                    return;
                }
                // A short last syllable can pass entirely between two Update frames.
                PublishCaption();
            }
            else if (silentGestureUntil == 0 || Time.unscaledTime < silentGestureUntil)
            {
                return;
            }
            if (!farewellActive) speaker.Listen(voiceListener);
            StopVoice(keepCaption: !farewellActive);
        }

        private void StopVoice(bool keepCaption = false)
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
            if (farewellActive && speaker) speaker.EndConversation();
            if (!keepCaption)
            {
                SpokenLine = "";
                VoiceUnavailable = false;
                if (speech) speech.gameObject.SetActive(false);
            }
            if (temporaryReply) Destroy(temporaryReply);
            temporaryReply = null;
            voice = null;
            speaker = null;
            voiceListener = null;
            pendingCaption = null;
            lineKind = VoiceLineKind.Greeting;
            silentGestureUntil = 0;
            nextHudDraw = 0;
        }

        private void ReleaseGreetings()
        {
            foreach (var clip in greetings.Values)
                if (clip) Destroy(clip);
            greetings.Clear();
        }
    }
}
