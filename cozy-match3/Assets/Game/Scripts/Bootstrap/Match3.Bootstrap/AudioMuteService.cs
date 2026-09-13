using System;
using Match3.Hud;
using Match3.Progression;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// The sound on/off button behind the HUD toggle. Mutes through
    /// <see cref="AudioListener"/> rather than the ambience source, so every sound added later
    /// goes quiet with it and nothing has to be told about the button.
    /// </summary>
    public sealed class AudioMuteService : IInitializable, IDisposable
    {
        private readonly SoundToggleView _view;
        private readonly IProgressStorage _storage;

        private bool _muted;

        private const string MutedKey = "audio.muted";

        public AudioMuteService(SoundToggleView view, IProgressStorage storage)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Initialize()
        {
            _muted = _storage.GetInt(MutedKey, 0) != 0;
            Apply();

            _view.Clicked += OnClicked;
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.Clicked -= OnClicked;
            }

            // AudioListener.volume is deliberately left where the player put it: it is a setting,
            // not scene state, and restoring it here would unmute behind their back.
        }

        private void Apply()
        {
            AudioListener.volume = _muted ? 0f : 1f;
            _view.SetMuted(_muted);
        }

        private void OnClicked()
        {
            _muted = !_muted;

            // §13: PlayerPrefs is IndexedDB on WebGL, so the write is flushed immediately.
            _storage.SetInt(MutedKey, _muted ? 1 : 0);
            _storage.Save();

            Apply();
        }
    }
}
