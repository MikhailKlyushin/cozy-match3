using System.Collections.Generic;
using Match3.Progression;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Progression
{
    /// <summary>
    /// GDD §13: two keys, and every write flushed. On WebGL PlayerPrefs is IndexedDB, so a write
    /// that was never flushed is simply gone when the page closes.
    /// </summary>
    public sealed class ProgressRepositoryTests
    {
        private const string CurrentLevelKey = "progress.currentLevel";
        private const string AllCompletedKey = "progress.allCompleted";

        private FakeProgressStorage _storage;
        private ProgressRepository _progress;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeProgressStorage();
            _progress = new ProgressRepository(_storage);
        }

        [Test]
        public void CurrentLevel_IsZeroUntilSomethingIsStored()
        {
            Assert.AreEqual(0, _progress.CurrentLevel);
            Assert.IsFalse(_progress.HasProgress);
            Assert.IsFalse(_progress.AllCompleted);
        }

        [Test]
        public void SetCurrentLevel_WritesTheDocumentedKeyAndFlushes()
        {
            _progress.SetCurrentLevel(7);

            Assert.AreEqual(7, _storage.GetInt(CurrentLevelKey, 0));
            Assert.AreEqual(7, _progress.CurrentLevel);
            Assert.IsTrue(_progress.HasProgress);
            Assert.AreEqual(0, _storage.UnflushedWrites, "Save is mandatory after every write (§13)");
        }

        [Test]
        public void SetAllCompleted_WritesTheDocumentedKeyAndFlushes()
        {
            _progress.SetAllCompleted(true);

            Assert.IsTrue(_progress.AllCompleted);
            Assert.AreEqual(1, _storage.GetInt(AllCompletedKey, 0));
            Assert.AreEqual(0, _storage.UnflushedWrites);

            _progress.SetAllCompleted(false);

            Assert.IsFalse(_progress.AllCompleted);
            Assert.AreEqual(0, _storage.UnflushedWrites);
        }

        [Test]
        public void Restart_ResetsBothKeysAndFlushes()
        {
            _progress.SetCurrentLevel(9);
            _progress.SetAllCompleted(true);

            _progress.Restart(1);

            Assert.AreEqual(1, _progress.CurrentLevel);
            Assert.IsFalse(_progress.AllCompleted);
            Assert.AreEqual(0, _storage.UnflushedWrites);
        }

        [Test]
        public void Clear_RemovesBothKeysAndFlushes()
        {
            _progress.SetCurrentLevel(5);
            _progress.SetAllCompleted(true);

            _progress.Clear();

            Assert.IsFalse(_storage.HasKey(CurrentLevelKey));
            Assert.IsFalse(_storage.HasKey(AllCompletedKey));
            Assert.IsFalse(_progress.HasProgress);
            Assert.AreEqual(0, _storage.UnflushedWrites);
        }

        [Test]
        public void EveryWritingCall_EndsWithExactlyOneSave()
        {
            _progress.SetCurrentLevel(2);
            _progress.SetAllCompleted(true);
            _progress.Restart(1);
            _progress.Clear();

            Assert.AreEqual(4, _storage.SaveCount, "one flush per repository call");
        }
    }

    /// <summary>
    /// Stand-in for PlayerPrefs so progression tests never touch the Editor's real prefs. Counts
    /// flushes, because an unflushed write is lost on WebGL (§13).
    /// </summary>
    internal sealed class FakeProgressStorage : IProgressStorage
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();

        public int SaveCount { get; private set; }

        public int WriteCount { get; private set; }

        /// <summary>Writes made since the last <see cref="Save"/>; must be 0 after every call.</summary>
        public int UnflushedWrites { get; private set; }

        public int GetInt(string key, int fallback) => _values.TryGetValue(key, out int value) ? value : fallback;

        public void SetInt(string key, int value)
        {
            _values[key] = value;
            WriteCount++;
            UnflushedWrites++;
        }

        public bool HasKey(string key) => _values.ContainsKey(key);

        public void DeleteKey(string key)
        {
            _values.Remove(key);
            WriteCount++;
            UnflushedWrites++;
        }

        public void Save()
        {
            SaveCount++;
            UnflushedWrites = 0;
        }
    }
}
