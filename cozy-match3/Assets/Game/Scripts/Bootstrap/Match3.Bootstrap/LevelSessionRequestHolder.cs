using System;
using Match3.Progression;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Hands the attempt's request to the LevelContext installer. A Zenject GameObjectContext
    /// cannot take constructor arguments, so the factory parks the request here and the installer
    /// picks it up while the sub-container builds (A08).
    /// </summary>
    public sealed class LevelSessionRequestHolder
    {
        private LevelSessionRequest _pending;
        private bool _hasPending;

        public void Set(in LevelSessionRequest request)
        {
            _pending = request;
            _hasPending = true;
        }

        /// <summary>Consumed exactly once, so a stale request can never leak into a later attempt.</summary>
        public LevelSessionRequest Take()
        {
            if (!_hasPending)
            {
                throw new InvalidOperationException(
                    "No pending level session request: the context was created outside LevelContextFactory.");
            }

            _hasPending = false;
            LevelSessionRequest request = _pending;
            _pending = default;
            return request;
        }
    }
}
