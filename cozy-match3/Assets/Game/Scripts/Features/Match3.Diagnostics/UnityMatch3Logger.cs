using Match3.Core;
using UnityEngine;

namespace Match3.Diagnostics
{
    /// <summary>
    /// Bridges the engine-free core logger onto UnityEngine.Debug. Errors here are broken
    /// invariants, never balance: CapHit and deadlock go through <see cref="Error"/> (E05, E19).
    /// </summary>
    public sealed class UnityMatch3Logger : IMatch3Logger
    {
        private const string Prefix = "[Match3] ";

        public void Info(string message) => Debug.Log(Prefix + message);

        public void Warn(string message) => Debug.LogWarning(Prefix + message);

        public void Error(string message) => Debug.LogError(Prefix + message);
    }
}
