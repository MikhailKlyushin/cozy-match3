using Match3.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Tests.EditMode.Hud
{
    /// <summary>
    /// Cycle mapping of <see cref="LoopedFlipbookView"/>: the sheet walks every frame at the
    /// configured rate, then the hold frame stays put until the cycle ends. The tween that drives
    /// the mapping belongs to a running scene and is not started here.
    /// </summary>
    public sealed class LoopedFlipbookViewTests
    {
        private const int FrameCount = 4;
        private const float FramesPerSecond = 10f;
        private const float HoldSeconds = 1f;

        private GameObject _root;
        private LoopedFlipbookView _flipbook;
        private Image _image;
        private Sprite[] _frames;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Flipbook", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _image = _root.GetComponent<Image>();
            _flipbook = _root.AddComponent<LoopedFlipbookView>();

            _frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                var texture = new Texture2D(2, 2);
                _frames[i] = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
                _frames[i].name = "Frame" + i.ToString();
            }

            Wire("_image", _image);
            Wire("_frames", _frames);
            WireFloat("_framesPerSecond", FramesPerSecond);
            WireFloat("_holdSeconds", HoldSeconds);
            WireInt("_holdFrame", 0);
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
        public void CycleDuration_IsTheSheetPlusTheHold()
        {
            Assert.AreEqual(FrameCount / FramesPerSecond + HoldSeconds, _flipbook.CycleDuration, 1e-4f);
        }

        [Test]
        public void SetCycleTime_GivesEveryFrameOneOverTheRate()
        {
            for (int i = 0; i < FrameCount; i++)
            {
                // The middle of each frame's slot on the timeline.
                float seconds = (i + 0.5f) / FramesPerSecond;
                _flipbook.SetCycleTime(seconds);
                Assert.AreSame(_frames[i], _image.sprite, "second " + seconds.ToString());
            }
        }

        [Test]
        public void SetCycleTime_RestsOnTheHoldFrame_AfterTheSheetIsDone()
        {
            float play = FrameCount / FramesPerSecond;

            _flipbook.SetCycleTime(play);
            Assert.AreSame(_frames[0], _image.sprite);

            _flipbook.SetCycleTime(play + HoldSeconds);
            Assert.AreSame(_frames[0], _image.sprite);
        }

        [Test]
        public void SetCycleTime_HoldsOnTheConfiguredFrame()
        {
            WireInt("_holdFrame", FrameCount - 1);

            _flipbook.SetCycleTime(FrameCount / FramesPerSecond);

            Assert.AreSame(_frames[FrameCount - 1], _image.sprite);
        }

        [Test]
        public void SetCycleTime_ClampsBelowZeroToTheFirstFrame()
        {
            _flipbook.SetCycleTime(-1f);

            Assert.AreSame(_frames[0], _image.sprite);
        }

        [Test]
        public void Play_WithoutRateOrHold_RestsOnTheHoldFrameAndDoesNotThrow()
        {
            WireFloat("_framesPerSecond", 0f);
            WireFloat("_holdSeconds", 0f);
            WireInt("_holdFrame", 1);

            Assert.DoesNotThrow(() => _flipbook.Play());
            Assert.AreSame(_frames[1], _image.sprite);
        }

        [Test]
        public void Play_WithoutFrames_DoesNotThrow()
        {
            Wire("_frames", new Sprite[0]);

            Assert.AreEqual(0, _flipbook.FrameCount);
            Assert.DoesNotThrow(() => _flipbook.Play());
            Assert.DoesNotThrow(() => _flipbook.Stop());
        }

        [Test]
        public void Stop_ReturnsTheViewToItsFirstFrame()
        {
            _flipbook.SetCycleTime(2.5f / FramesPerSecond);

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

        private void WireFloat(string fieldName, float value)
        {
            var so = new UnityEditor.SerializedObject(_flipbook);
            so.FindProperty(fieldName).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void WireInt(string fieldName, int value)
        {
            var so = new UnityEditor.SerializedObject(_flipbook);
            so.FindProperty(fieldName).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
