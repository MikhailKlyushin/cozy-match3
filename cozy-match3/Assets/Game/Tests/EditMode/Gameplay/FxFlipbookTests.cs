using Match3.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>
    /// Frame mapping of <see cref="FxFlipbook"/> (T27). The tween that drives it belongs to a
    /// running scene; what is checked here is that the mapping walks every frame once and stops
    /// on the last, which is the half the ANIMATE barrier depends on (§5.1).
    /// </summary>
    public sealed class FxFlipbookTests
    {
        private const int FrameCount = 4;

        private GameObject _root;
        private FxFlipbook _flipbook;
        private Image _image;
        private Sprite[] _frames;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Flipbook", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _image = _root.GetComponent<Image>();
            _flipbook = _root.AddComponent<FxFlipbook>();

            _frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                var texture = new Texture2D(2, 2);
                _frames[i] = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
                _frames[i].name = "Frame" + i.ToString();
            }

            Wire("_image", _image);
            Wire("_frames", _frames);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _frames.Length; i++)
            {
                Object.DestroyImmediate(_frames[i].texture);
                Object.DestroyImmediate(_frames[i]);
            }

            Object.DestroyImmediate(_root);
        }

        [Test]
        public void FrameCount_IsTheSheetLength()
        {
            Assert.AreEqual(FrameCount, _flipbook.FrameCount);
        }

        [Test]
        public void SetProgress_WalksEveryFrameInOrder()
        {
            for (int i = 0; i < FrameCount; i++)
            {
                // The middle of each frame's share of the timeline.
                float progress = (i + 0.5f) / FrameCount;
                _flipbook.SetProgress(progress);
                Assert.AreSame(_frames[i], _image.sprite, "progress " + progress.ToString());
            }
        }

        [Test]
        public void SetProgress_StopsOnTheLastFrame_AtAndPastTheEnd()
        {
            _flipbook.SetProgress(1f);
            Assert.AreSame(_frames[FrameCount - 1], _image.sprite);

            _flipbook.SetProgress(2f);
            Assert.AreSame(_frames[FrameCount - 1], _image.sprite);
        }

        [Test]
        public void SetProgress_ClampsBelowZeroToTheFirstFrame()
        {
            _flipbook.SetProgress(-1f);
            Assert.AreSame(_frames[0], _image.sprite);
        }

        [Test]
        public void Play_WithoutDuration_RestsOnTheFirstFrameAndDoesNotThrow()
        {
            _flipbook.SetProgress(1f);

            Assert.DoesNotThrow(() => _flipbook.Play(0f));
            Assert.AreSame(_frames[0], _image.sprite);
        }

        [Test]
        public void Play_WithoutFrames_DoesNotThrow()
        {
            Wire("_frames", new Sprite[0]);

            Assert.AreEqual(0, _flipbook.FrameCount);
            Assert.DoesNotThrow(() => _flipbook.Play(0.2f));
            Assert.DoesNotThrow(() => _flipbook.Stop());
        }

        [Test]
        public void Stop_ReturnsThePooledInstanceToItsFirstFrame()
        {
            _flipbook.SetProgress(1f);

            _flipbook.Stop();

            Assert.AreSame(_frames[0], _image.sprite);
        }

        private void Wire(string fieldName, Object value)
        {
            var so = new UnityEditor.SerializedObject(_flipbook);
            so.FindProperty(fieldName).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void Wire(string fieldName, Sprite[] values)
        {
            var so = new UnityEditor.SerializedObject(_flipbook);
            UnityEditor.SerializedProperty array = so.FindProperty(fieldName);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
