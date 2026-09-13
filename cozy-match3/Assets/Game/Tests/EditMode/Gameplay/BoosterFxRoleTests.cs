using Match3.Content;
using Match3.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>
    /// The fallback contract of the FX roles (T31): a profile authored before the roles existed
    /// carries only <c>_activationFx</c>, and every role has to keep resolving to it, or splitting
    /// the roles would silently turn off the effects of every existing project.
    /// </summary>
    public sealed class BoosterFxRoleTests
    {
        private const BoosterType Booster = BoosterType.Bomb;

        private ChipVisualProfile _profile;
        private GameObject _activation;
        private GameObject _beam;
        private GameObject _burst;
        private GameObject _impact;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<ChipVisualProfile>();
            _activation = new GameObject("Activation");
            _beam = new GameObject("Beam");
            _burst = new GameObject("Burst");
            _impact = new GameObject("Impact");

            var so = new SerializedObject(_profile);
            SerializedProperty boosters = so.FindProperty("_boosters");
            boosters.arraySize = 1;

            SerializedProperty entry = boosters.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_booster").enumValueIndex = (int)Booster;
            entry.FindPropertyRelative("_activationFx").objectReferenceValue = _activation;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_impact);
            Object.DestroyImmediate(_burst);
            Object.DestroyImmediate(_beam);
            Object.DestroyImmediate(_activation);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void EmptyRoles_FallBackToTheActivationEffect()
        {
            Assert.AreSame(_activation, _profile.GetBoosterFx(Booster));
            Assert.AreSame(_activation, _profile.GetBoosterBeamFx(Booster));
            Assert.AreSame(_activation, _profile.GetBoosterBurstFx(Booster));
            Assert.AreSame(_activation, _profile.GetBoosterImpactFx(Booster));
        }

        [Test]
        public void FilledRoles_ReturnTheirOwnEffect()
        {
            AssignRoles();

            Assert.AreSame(_activation, _profile.GetBoosterFx(Booster));
            Assert.AreSame(_beam, _profile.GetBoosterBeamFx(Booster));
            Assert.AreSame(_burst, _profile.GetBoosterBurstFx(Booster));
            Assert.AreSame(_impact, _profile.GetBoosterImpactFx(Booster));
        }

        [Test]
        public void UnknownBooster_HasNoEffectInAnyRole()
        {
            AssignRoles();

            Assert.IsNull(_profile.GetBoosterFx(BoosterType.Rainbow));
            Assert.IsNull(_profile.GetBoosterBeamFx(BoosterType.Rainbow));
            Assert.IsNull(_profile.GetBoosterBurstFx(BoosterType.Rainbow));
            Assert.IsNull(_profile.GetBoosterImpactFx(BoosterType.Rainbow));
        }

        private void AssignRoles()
        {
            var so = new SerializedObject(_profile);
            SerializedProperty entry = so.FindProperty("_boosters").GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_beamFx").objectReferenceValue = _beam;
            entry.FindPropertyRelative("_burstFx").objectReferenceValue = _burst;
            entry.FindPropertyRelative("_impactFx").objectReferenceValue = _impact;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
