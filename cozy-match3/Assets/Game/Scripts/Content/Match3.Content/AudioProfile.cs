using UnityEngine;

namespace Match3.Content
{
    /// <summary>
    /// Audio content in one asset. §11.3 designs no sound at all, so the ambience carries its own
    /// tunables here rather than as literals inside the service that plays it.
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Audio Profile", fileName = "AudioProfile")]
    public sealed class AudioProfile : ScriptableObject
    {
        [Header("Ambience")]
        [SerializeField] private AudioClip _ambient;
        [SerializeField, Range(0f, 1f)] private float _ambientVolume = 0.35f;

        /// <summary>Seconds; zero starts the loop at full volume.</summary>
        [SerializeField] private float _ambientFadeIn = 1.5f;

        public AudioClip Ambient => _ambient;

        public float AmbientVolume => _ambientVolume;

        public float AmbientFadeIn => _ambientFadeIn;
    }
}
