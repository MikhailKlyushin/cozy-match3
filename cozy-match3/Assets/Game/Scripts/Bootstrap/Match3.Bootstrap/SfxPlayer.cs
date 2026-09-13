using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Content;
using Match3.Core;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// The one-shot sound layer: a fixed pool of 2D voices, the per-id throttle and the pitch
    /// ladder. It owns its sources outright, the way <see cref="AmbientAudioService"/> does, and
    /// mutes with everything else through <see cref="AudioListener"/> (§11.2).
    /// </summary>
    public sealed class SfxPlayer : ISfxPlayer, IInitializable, IDisposable
    {
        private readonly AudioProfile _profile;
        private readonly IMatch3Logger _logger;

        /// <summary>
        /// Own stream, deliberately not the attempt's <see cref="IRandom"/>: sound must not
        /// consume the gameplay RNG, whose call order is part of the determinism contract (D12).
        /// Seeded rather than <c>UnityEngine.Random</c> so nothing here can drift a replay.
        /// </summary>
        private readonly IRandom _jitter = new DeterministicRandom(JitterSeed);

        private readonly SfxThrottle _throttle = new SfxThrottle();

        /// <summary>One warning per missing clip: a cascade would otherwise log it per chip.</summary>
        private readonly bool[] _warned = new bool[IdCount];

        private AudioSource[] _voices;
        private GameObject _root;
        private int _nextVoice;
        private bool _disposed;

        private const int IdCount = (int)SfxId.LevelLost + 1;

        /// <summary>Unity's own pitch range; a jitter that walks outside it is silently clamped.</summary>
        private const float MinPitch = 0.5f;
        private const float MaxPitch = 2f;

        /// <summary>Any constant: the stream only has to be varied, never unpredictable.</summary>
        private const int JitterSeed = 20993;

        /// <summary>Resolution of one jitter draw, in steps across the spread.</summary>
        private const int JitterSteps = 1000;

        public SfxPlayer(AudioProfile profile, IMatch3Logger logger)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize() => EnsureVoices();

        public void Play(SfxId id) => Play(id, 1f, 0f, CancellationToken.None);

        public void PlayLadder(SfxId id, int rung)
            => Play(id, _profile.LadderPitch(rung), 0f, CancellationToken.None);

        public void Play(SfxId id, float pitchScale, float delay, CancellationToken ct)
        {
            if (_disposed || id == SfxId.None)
            {
                return;
            }

            if (!_profile.TryGet(id, out AudioProfile.SfxEntry entry))
            {
                WarnOnce(id);
                return;
            }

            float wait = Mathf.Max(0f, delay);

            // Booked for when it will be heard rather than for now, so a column of landings is
            // thinned by the time between them instead of collapsing into one tap.
            if (!_throttle.TryTake(id, Time.unscaledTime + wait, entry.MinInterval))
            {
                return;
            }

            if (wait > 0f)
            {
                // Not AudioSource.PlayDelayed: on WebGL it drops the delay and plays at once, so
                // the landing would sound where the fall starts. The wait is ours instead.
                PlayAfterAsync(entry, pitchScale, wait, ct).Forget();
                return;
            }

            PlayNow(entry, pitchScale);
        }

        public void Dispose()
        {
            _disposed = true;

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _voices = null;
        }

        private async UniTaskVoid PlayAfterAsync(
            AudioProfile.SfxEntry entry,
            float pitchScale,
            float delay,
            CancellationToken ct)
        {
            bool canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(delay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct)
                .SuppressCancellationThrow();

            if (canceled || _disposed)
            {
                return;
            }

            PlayNow(entry, pitchScale);
        }

        private void PlayNow(AudioProfile.SfxEntry entry, float pitchScale)
        {
            AudioSource voice = NextVoice();
            if (voice == null)
            {
                return;
            }

            voice.clip = entry.Clip;
            voice.volume = entry.Volume * _profile.SfxVolume;
            voice.pitch = Mathf.Clamp(pitchScale * Jitter(entry.PitchJitter), MinPitch, MaxPitch);
            voice.Play();
        }

        private float Jitter(float spread)
        {
            if (spread <= 0f)
            {
                return 1f;
            }

            float unit = (_jitter.NextInt(JitterSteps + 1) / (float)JitterSteps * 2f) - 1f;
            return 1f + (spread * unit);
        }

        /// <summary>
        /// A free voice, or the oldest one round-robin. Stealing is deliberate: dropping the
        /// sound instead would silence exactly the loudest moments, which is when the pool fills.
        /// </summary>
        private AudioSource NextVoice()
        {
            EnsureVoices();

            if (_voices == null || _voices.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < _voices.Length; i++)
            {
                int index = (_nextVoice + i) % _voices.Length;
                AudioSource candidate = _voices[index];

                if (candidate != null && !candidate.isPlaying)
                {
                    _nextVoice = (index + 1) % _voices.Length;
                    return candidate;
                }
            }

            AudioSource stolen = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return stolen;
        }

        /// <summary>
        /// The pool is built here rather than authored into the scene: it carries no setting an
        /// author would ever touch, and a scene generator that rebuilds it would only be one more
        /// thing to keep in sync.
        /// </summary>
        private void EnsureVoices()
        {
            if (_disposed || _root != null)
            {
                return;
            }

            int count = Mathf.Max(1, _profile.SfxVoices);
            _root = new GameObject("SfxVoices");
            _voices = new AudioSource[count];

            for (int i = 0; i < count; i++)
            {
                var voice = _root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;

                // Every sound of this game belongs to the board, which fills the screen: a 3D
                // voice would pan by where the listener happens to stand.
                voice.spatialBlend = 0f;

                _voices[i] = voice;
            }
        }

        private void WarnOnce(SfxId id)
        {
            if (_warned[(int)id])
            {
                return;
            }

            _warned[(int)id] = true;
            _logger.Warn("The audio profile carries no clip for " + id + "; that moment stays silent.");
        }
    }
}
