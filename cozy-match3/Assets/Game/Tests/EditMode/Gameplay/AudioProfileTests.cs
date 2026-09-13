using Match3.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>
    /// The two contracts every caller of the audio profile leans on: an entry without a clip
    /// counts as absent, so a half-filled profile plays silently instead of throwing mid-cascade,
    /// and the pitch ladder stops climbing at its cap.
    /// </summary>
    public sealed class AudioProfileTests
    {
        private AudioProfile _profile;
        private AudioClip _clip;

        /// <summary>Matches the defaults in <see cref="AudioProfile"/>: 0.06 per rung, capped 1.35.</summary>
        private const float LadderStep = 0.06f;
        private const float LadderCap = 1.35f;

        private const float Tolerance = 0.0001f;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<AudioProfile>();
            _clip = AudioClip.Create("Test", 64, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_clip);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void UnknownId_HasNoEntry()
        {
            WriteEntry(SfxId.Swap, _clip);

            Assert.IsFalse(_profile.TryGet(SfxId.Bomb, out AudioProfile.SfxEntry entry));
            Assert.IsNull(entry);
        }

        [Test]
        public void EntryWithoutClip_CountsAsAbsent()
        {
            WriteEntry(SfxId.Swap, null);

            Assert.IsFalse(_profile.TryGet(SfxId.Swap, out _));
        }

        [Test]
        public void EntryWithClip_IsFound()
        {
            WriteEntry(SfxId.Swap, _clip);

            Assert.IsTrue(_profile.TryGet(SfxId.Swap, out AudioProfile.SfxEntry entry));
            Assert.AreSame(_clip, entry.Clip);
        }

        [Test]
        public void FirstRung_PlaysAtTheClipsOwnPitch()
        {
            Assert.AreEqual(1f, _profile.LadderPitch(0), Tolerance);

            // A negative rung is not a caller's mistake to punish: a cascade counts from 0 and
            // anything below it simply has no climb.
            Assert.AreEqual(1f, _profile.LadderPitch(-1), Tolerance);
        }

        [Test]
        public void EachRung_ClimbsByOneStep()
        {
            Assert.AreEqual(1f + LadderStep, _profile.LadderPitch(1), Tolerance);
            Assert.AreEqual(1f + (LadderStep * 3f), _profile.LadderPitch(3), Tolerance);
        }

        [Test]
        public void DeepCascade_StopsAtTheCap()
        {
            Assert.AreEqual(LadderCap, _profile.LadderPitch(20), Tolerance);
        }

        private void WriteEntry(SfxId id, AudioClip clip)
        {
            var so = new SerializedObject(_profile);
            SerializedProperty entries = so.FindProperty("_sfx");
            entries.arraySize = 1;

            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_id").enumValueIndex = (int)id;
            entry.FindPropertyRelative("_clip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
