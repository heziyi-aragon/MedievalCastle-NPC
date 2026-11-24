using UnityEngine;

namespace MedievalCastle.Npc.Demo
{
    internal static class NpcVoiceProfile
    {
        // Each character owns a dedicated spatial source. Configure that source, so
        // a cached greeting and a freshly generated reply sound like the same NPC.
        internal static void Apply(AudioSource source, string role)
        {
            switch (role)
            {
                case "merchant":
                    source.pitch = .90f;
                    if (!source.TryGetComponent<AudioLowPassFilter>(out var warmth))
                        warmth = source.gameObject.AddComponent<AudioLowPassFilter>();
                    warmth.cutoffFrequency = 6000f;
                    warmth.lowpassResonanceQ = 1f;
                    break;
                case "knight":
                    source.pitch = 1.045f;
                    if (!source.TryGetComponent<AudioHighPassFilter>(out var clarity))
                        clarity = source.gameObject.AddComponent<AudioHighPassFilter>();
                    clarity.cutoffFrequency = 150f;
                    clarity.highpassResonanceQ = 1f;
                    break;
                default:
                    source.pitch = .94f;
                    break;
            }
        }
    }
}
