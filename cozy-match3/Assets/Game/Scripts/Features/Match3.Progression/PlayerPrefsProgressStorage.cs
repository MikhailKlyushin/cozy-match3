using UnityEngine;

namespace Match3.Progression
{
    public sealed class PlayerPrefsProgressStorage : IProgressStorage
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        public void Save() => PlayerPrefs.Save();
    }
}
