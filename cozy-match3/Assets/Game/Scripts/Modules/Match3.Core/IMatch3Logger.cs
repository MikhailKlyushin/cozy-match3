namespace Match3.Core
{
    /// <summary>
    /// Core logger: lets noEngineReferences modules report broken invariants without knowing
    /// about UnityEngine.Debug (I1). Not for the hot path — arguments are built even when
    /// logging is off (§14).
    /// </summary>
    public interface IMatch3Logger
    {
        void Info(string message);

        void Warn(string message);

        /// <summary>A broken invariant, always a bug: CapHit, deadlock (E05, E19, §14).</summary>
        void Error(string message);
    }

    public sealed class NullLogger : IMatch3Logger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
