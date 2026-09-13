using System;
using DG.Tweening;
using Match3.Content;
using Match3.Core;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Plays the looping ambience for as long as the game scene lives. Nothing in the game drives
    /// it, so it owns its source outright instead of going through a pool or a mixer group.
    /// </summary>
    public sealed class AmbientAudioService : IInitializable, IDisposable
    {
        private readonly AudioProfile _profile;
        private readonly AudioSource _source;
        private readonly IMatch3Logger _logger;

        public AmbientAudioService(AudioProfile profile, AudioSource source, IMatch3Logger logger)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _source = source != null ? source : throw new ArgumentNullException(nameof(source));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize()
        {
            AudioClip clip = _profile.Ambient;
            if (clip == null)
            {
                _logger.Warn("The audio profile carries no ambience clip; the scene stays silent.");
                return;
            }

            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;

            // Also set on the source in the scene, and repeated here because a clip imported as 3D
            // would otherwise fade with the listener's distance from the origin.
            _source.spatialBlend = 0f;

            float target = Mathf.Clamp01(_profile.AmbientVolume);
            float fade = _profile.AmbientFadeIn;

            _source.volume = fade > 0f ? 0f : target;

            // On WebGL the browser keeps the audio context suspended until the first input; Unity
            // resumes it on that gesture and the loop is already running by then.
            _source.Play();

            if (fade > 0f)
            {
                _source.DOFade(target, fade).SetLink(_source.gameObject);
            }
        }

        public void Dispose()
        {
            if (_source == null)
            {
                return;
            }

            _source.DOKill();
            _source.Stop();
        }
    }
}
