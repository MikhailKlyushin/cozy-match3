namespace Match3.Progression
{
    /// <summary>
    /// Key-value sink behind <see cref="ProgressRepository"/>. It exists so progression can be
    /// covered by edit-mode tests without touching the real PlayerPrefs of the Editor.
    /// </summary>
    public interface IProgressStorage
    {
        int GetInt(string key, int fallback);

        void SetInt(string key, int value);

        bool HasKey(string key);

        void DeleteKey(string key);

        /// <summary>
        /// Must be called after every write: on WebGL PlayerPrefs is IndexedDB, and without an
        /// explicit flush closing the page loses the write (§13).
        /// </summary>
        void Save();
    }
}
