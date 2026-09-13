using System;
using UnityEngine;

namespace Match3.Content
{
    /// <summary>
    /// Audio content in one asset: the ambience and one entry per <see cref="SfxId"/>. §11.3
    /// designs no sound at all, so the tunables live here rather than as literals inside the
    /// services that play them.
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Audio Profile", fileName = "AudioProfile")]
    public sealed class AudioProfile : ScriptableObject
    {
        [Header("Ambience")]
        [SerializeField] private AudioClip _ambient;
        [SerializeField, Range(0f, 1f)] private float _ambientVolume = 0.35f;

        /// <summary>Seconds; zero starts the loop at full volume.</summary>
        [SerializeField] private float _ambientFadeIn = 1.5f;

        [Header("Sound effects")]
        [SerializeField] private SfxEntry[] _sfx = Array.Empty<SfxEntry>();

        /// <summary>Master trim over every effect, so the whole layer sits under the ambience.</summary>
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

        /// <summary>
        /// Voices the pool holds. A cascade step destroys a dozen chips at once and the throttle
        /// already collapses those into a few sounds; the rest of the budget covers a step whose
        /// boosters, landings and goal ticks overlap.
        /// </summary>
        [SerializeField, Range(4, 32)] private int _sfxVoices = 16;

        /// <summary>Pitch added per rung, so a deeper cascade or a longer tick series climbs.</summary>
        [Header("Pitch ladder")]
        [SerializeField, Range(0f, 0.3f)] private float _pitchLadderStep = 0.06f;

        [SerializeField, Range(1f, 2f)] private float _pitchLadderCap = 1.35f;

        public AudioClip Ambient => _ambient;

        public float AmbientVolume => _ambientVolume;

        public float AmbientFadeIn => _ambientFadeIn;

        public float SfxVolume => _sfxVolume;

        public int SfxVoices => _sfxVoices;

        /// <summary>
        /// Pitch at rung <paramref name="rung"/> of the ladder, counted from 0. The climb is
        /// capped: past a handful of rungs a rising scale turns into a squeak.
        /// </summary>
        public float LadderPitch(int rung)
        {
            if (rung <= 0)
            {
                return 1f;
            }

            return Mathf.Min(1f + (_pitchLadderStep * rung), _pitchLadderCap);
        }

        /// <summary>False when the id has no entry or the entry carries no clip - both legal.</summary>
        public bool TryGet(SfxId id, out SfxEntry entry)
        {
            for (int i = 0; i < _sfx.Length; i++)
            {
                SfxEntry candidate = _sfx[i];
                if (candidate != null && candidate.Id == id)
                {
                    entry = candidate;
                    return candidate.Clip != null;
                }
            }

            entry = null;
            return false;
        }

        [Serializable]
        public sealed class SfxEntry
        {
            [SerializeField] private SfxId _id = SfxId.None;
            [SerializeField] private AudioClip _clip;
            [SerializeField, Range(0f, 1f)] private float _volume = 1f;

            /// <summary>
            /// Seconds the id stays silent after it plays. This is what keeps a cascade from
            /// firing twenty identical pops inside one frame: they arrive together, and past the
            /// first few the ear hears clipping rather than chips.
            /// </summary>
            [SerializeField, Range(0f, 0.5f)] private float _minInterval;

            /// <summary>Random pitch spread, so a repeated sound is not a machine gun.</summary>
            [SerializeField, Range(0f, 0.5f)] private float _pitchJitter;

            public SfxId Id => _id;

            public AudioClip Clip => _clip;

            public float Volume => _volume;

            public float MinInterval => _minInterval;

            public float PitchJitter => _pitchJitter;
        }
    }
}
