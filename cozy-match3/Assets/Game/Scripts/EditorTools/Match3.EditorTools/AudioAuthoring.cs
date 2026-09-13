using System.IO;
using Match3.Content;
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
        /// Mix defaults for a newly added effect. The loud, rare moments sit above the constant
        /// ones: a pop plays a dozen times a turn and a win sting once a level.
        /// <para>
        /// <c>MinInterval</c> is the silence an id holds after it sounds, and it does most of the
        /// work: a cascade step kills its chips in a single frame, so one pop is heard for the
        /// step instead of twelve stacked into a click. Sounds that arrive spread out over time -
        /// landings, rainbow beams - pass through it and stay a series.
        /// </para>
        /// </summary>
        private static readonly SfxDefault[] Defaults =
        {
            new SfxDefault(SfxId.Swap, 0.60f, 0.02f, 0.04f),
            new SfxDefault(SfxId.SwapRejected, 0.70f, 0.05f, 0f),
            new SfxDefault(SfxId.ChipDestroyed, 0.55f, 0.035f, 0.06f),
            new SfxDefault(SfxId.ChipLand, 0.35f, 0.05f, 0.08f),
            new SfxDefault(SfxId.BoosterSpawned, 0.70f, 0.05f, 0.03f),
            new SfxDefault(SfxId.Rocket, 0.70f, 0.05f, 0.03f),
            new SfxDefault(SfxId.Bomb, 0.85f, 0.05f, 0.02f),
            new SfxDefault(SfxId.Rainbow, 0.80f, 0.10f, 0f),
            new SfxDefault(SfxId.RainbowBeam, 0.30f, 0.05f, 0.08f),
            new SfxDefault(SfxId.Airplane, 0.70f, 0.05f, 0.03f),
            new SfxDefault(SfxId.ElementDamaged, 0.50f, 0.04f, 0.06f),
            new SfxDefault(SfxId.ElementDestroyed, 0.70f, 0.04f, 0.04f),
            new SfxDefault(SfxId.GoalTick, 0.50f, 0.05f, 0f),
            new SfxDefault(SfxId.LevelWon, 0.90f, 0f, 0f),
            new SfxDefault(SfxId.LevelLost, 0.90f, 0f, 0f)
        };

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

        /// <summary>Defaults for <paramref name="id"/>; volume 1 and no throttle when it has none.</summary>
        internal static SfxDefault DefaultsFor(SfxId id)
        {
            for (int i = 0; i < Defaults.Length; i++)
            {
                if (Defaults[i].Id == id)
                {
                    return Defaults[i];
                }
            }

            return new SfxDefault(id, 1f, 0f, 0f);
        }

        internal static AudioClip LoadAmbient() => LoadClip(AmbientClipName);

        /// <summary>
        /// The clip of an effect, by the convention that an id names its file. A missing one is
        /// reported and left null: the moment then plays without sound, which is legal.
        /// </summary>
        internal static AudioClip LoadSfx(SfxId id) => LoadClip("SFX_" + id);

        private static AudioClip LoadClip(string clipName)
        {
            string path = AudioFolder + "/" + clipName + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError("[Match3] Missing audio clip: " + path);
            }

            return clip;
        }

        /// <summary>
        /// Vorbis everywhere - the browser decodes it (`optimize-audio`, Web row). The load type
        /// splits by role: the ambience stays compressed in memory, because WebGL has no streaming
        /// at all and silently falls back, while decompressing half a minute of stereo would cost
        /// megabytes of PCM for a track that is never seeked. Every effect is decompressed on
        /// load instead - the longest is 1.9 s, so the whole layer is well under a megabyte, and
        /// a cascade must not pay a Vorbis decode per pop.
        /// </summary>
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError("[Match3] No AudioImporter for " + path);
                return;
            }

            bool isAmbient = Path.GetFileNameWithoutExtension(path) == AmbientClipName;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isAmbient
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = true;

            // Effects are 2D and centred, so a stereo one would only cost memory; the ambience is
            // the one clip whose width is the point.
            bool forceToMono = !isAmbient;

            // Spatial blend is deliberately absent: AudioImporter.threeD is gone in Unity 6 and the
            // importer no longer decides it. Every source here sets spatialBlend to 0 itself.
            bool changed = importer.forceToMono != forceToMono
                || importer.ambisonic
                || importer.loadInBackground
                || !settings.Equals(importer.defaultSampleSettings);

            if (!changed)
            {
                return;
            }

            importer.forceToMono = forceToMono;
            importer.ambisonic = false;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        /// <summary>Mix values written into a profile entry the first time it is created.</summary>
        internal readonly struct SfxDefault
        {
            internal readonly SfxId Id;
            internal readonly float Volume;
            internal readonly float MinInterval;
            internal readonly float PitchJitter;

            internal SfxDefault(SfxId id, float volume, float minInterval, float pitchJitter)
            {
                Id = id;
                Volume = volume;
                MinInterval = minInterval;
                PitchJitter = pitchJitter;
            }
        }
    }
}
