using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Keeps the import settings of the audio folder under code control, for the same reason
    /// <see cref="ArtAuthoring"/> does it for textures: they follow from how a clip is used, and a
    /// hand-edited .meta loses them on the next reimport.
    /// </summary>
    internal static class AudioAuthoring
    {
        internal const string AudioFolder = "Assets/Game/Content/Gameplay/SFX";
        internal const string AmbientClipName = "SFX_Ambient";

        /// <summary>
        /// Brings every clip in the audio folder to the settings WebGL needs. Idempotent, and it
        /// never touches a single sample.
        /// </summary>
        [MenuItem("Match3/Authoring/Apply Audio Import Settings")]
        public static void ApplyImportSettings()
        {
            if (!Directory.Exists(AudioFolder))
            {
                Debug.LogWarning("[Match3] No audio folder at " + AudioFolder);
                return;
            }

            int count = 0;
            foreach (string path in Directory.EnumerateFiles(AudioFolder, "*.ogg", SearchOption.TopDirectoryOnly))
            {
                Configure(path.Replace('\\', '/'));
                count++;
            }

            Debug.Log("[Match3] Audio import settings applied: " + count.ToString() + " clips");
        }

        internal static AudioClip LoadAmbient()
        {
            string path = AudioFolder + "/" + AmbientClipName + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError("[Match3] Missing audio clip: " + path);
            }

            return clip;
        }

        /// <summary>
        /// Compressed in memory, not streamed and not decompressed on load: WebGL has no streaming
        /// at all and silently falls back, while decompressing a minute of ambience on load costs
        /// megabytes of PCM for a track that is never seeked.
        /// </summary>
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError("[Match3] No AudioImporter for " + path);
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = true;

            // Spatial blend is deliberately absent: AudioImporter.threeD is gone in Unity 6 and the
            // importer no longer decides it. Every source here sets spatialBlend to 0 itself.
            bool changed = importer.forceToMono
                || importer.ambisonic
                || importer.loadInBackground
                || !settings.Equals(importer.defaultSampleSettings);

            if (!changed)
            {
                return;
            }

            importer.forceToMono = false;
            importer.ambisonic = false;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
