using UnityEngine;

namespace Match3.Content
{
    /// <summary>
    /// Every duration from GDD §11.3 in one asset. A magic number in animation code is a review
    /// finding; the model itself knows nothing about time (A01), so only presentation reads this.
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Timing Profile", fileName = "TimingProfile")]
    public sealed class TimingProfile : ScriptableObject
    {
        [Header("Input and swap")]
        [SerializeField] private float _swapDuration = 0.15f;
        [SerializeField] private float _rejectedSwapOut = 0.10f;
        [SerializeField] private float _rejectedSwapBack = 0.10f;

        [Header("Chip destruction (total 0.20 s)")]
        [SerializeField] private float _destroyPunchDuration = 0.06f;
        [SerializeField] private float _destroyPunchScale = 1.15f;
        [SerializeField] private float _destroyFadeDuration = 0.14f;

        [Header("Falling")]
        [SerializeField] private float _fallAcceleration = 40f;
        [SerializeField] private float _fallMaxSpeed = 18f;
        [SerializeField] private float _landingSquashDuration = 0.06f;
        [SerializeField] private float _neighbourFallStagger = 0.03f;

        [Header("Cascade and chains")]
        [SerializeField] private float _stepPause = 0.08f;
        [SerializeField] private float _waveBarrier = 0.12f;
        [SerializeField] private float _massTransformStagger = 0.07f;

        [Header("Boosters")]
        [SerializeField] private float _rocketCellDuration = 0.03f;
        [SerializeField] private float _rocketShakeDuration = 0.05f;
        [SerializeField] private float _rocketShakeAmplitude = 4f;
        [SerializeField] private float _bombWindUp = 0.10f;
        [SerializeField] private float _bombWindUpScale = 1.2f;
        [SerializeField] private float _bombShockwave = 0.25f;
        [SerializeField] private float _bombShakeDuration = 0.12f;
        [SerializeField] private float _bombShakeAmplitude = 10f;
        [SerializeField] private float _rainbowWindUp = 0.15f;
        [SerializeField] private float _rainbowBeamStagger = 0.04f;
        [SerializeField] private float _rainbowHitDelay = 0.10f;
        [SerializeField] private float _airplaneWindUp = 0.10f;
        [SerializeField] private float _airplaneFlight = 0.40f;

        [Header("Post-turn and HUD")]
        [SerializeField] private float _goalTickPerUnit = 0.15f;
        [SerializeField] private float _goalTickTotalCap = 1.0f;
        [SerializeField] private float _cyclingBoxMorph = 0.25f;
        [SerializeField] private float _shuffleScatter = 0.35f;
        [SerializeField] private float _shuffleGather = 0.35f;
        [SerializeField] private float _movesBonusStagger = 0.15f;
        [SerializeField] private float _lowMovesPulseCycle = 0.6f;

        [Header("Hint (§5.5)")]
        [SerializeField] private float _hintPulseCycle = 0.5f;
        [SerializeField] private int _hintPulseCycles = 3;
        [SerializeField] private float _hintPulseScale = 1.08f;
        [SerializeField] private float _hintArrowFadeIn = 0.2f;
        [SerializeField] private float _hintRepeatPause = 2.0f;
        [SerializeField] private float _hintHide = 0.1f;

        public float SwapDuration => _swapDuration;

        public float RejectedSwapOut => _rejectedSwapOut;

        public float RejectedSwapBack => _rejectedSwapBack;

        public float DestroyPunchDuration => _destroyPunchDuration;

        public float DestroyPunchScale => _destroyPunchScale;

        public float DestroyFadeDuration => _destroyFadeDuration;

        /// <summary>Total chip destruction time; the ANIMATE barrier waits at least this long.</summary>
        public float DestroyTotalDuration => _destroyPunchDuration + _destroyFadeDuration;

        /// <summary>Cells per second squared.</summary>
        public float FallAcceleration => _fallAcceleration;

        /// <summary>Cells per second.</summary>
        public float FallMaxSpeed => _fallMaxSpeed;

        public float LandingSquashDuration => _landingSquashDuration;

        public float NeighbourFallStagger => _neighbourFallStagger;

        public float StepPause => _stepPause;

        public float WaveBarrier => _waveBarrier;

        public float MassTransformStagger => _massTransformStagger;

        public float RocketCellDuration => _rocketCellDuration;

        public float RocketShakeDuration => _rocketShakeDuration;

        public float RocketShakeAmplitude => _rocketShakeAmplitude;

        public float BombWindUp => _bombWindUp;

        public float BombWindUpScale => _bombWindUpScale;

        public float BombShockwave => _bombShockwave;

        public float BombShakeDuration => _bombShakeDuration;

        public float BombShakeAmplitude => _bombShakeAmplitude;

        public float RainbowWindUp => _rainbowWindUp;

        public float RainbowBeamStagger => _rainbowBeamStagger;

        public float RainbowHitDelay => _rainbowHitDelay;

        public float AirplaneWindUp => _airplaneWindUp;

        public float AirplaneFlight => _airplaneFlight;

        public float GoalTickPerUnit => _goalTickPerUnit;

        public float GoalTickTotalCap => _goalTickTotalCap;

        public float CyclingBoxMorph => _cyclingBoxMorph;

        public float ShuffleScatter => _shuffleScatter;

        public float ShuffleGather => _shuffleGather;

        public float MovesBonusStagger => _movesBonusStagger;

        public float LowMovesPulseCycle => _lowMovesPulseCycle;

        public float HintPulseCycle => _hintPulseCycle;

        public int HintPulseCycles => _hintPulseCycles;

        public float HintPulseScale => _hintPulseScale;

        public float HintArrowFadeIn => _hintArrowFadeIn;

        public float HintRepeatPause => _hintRepeatPause;

        public float HintHide => _hintHide;

        /// <summary>
        /// Per-unit tick compressed so a large jump still lands inside the 1.0 s cap (§11.3).
        /// </summary>
        public float GoalTickStep(int units)
        {
            if (units <= 0)
            {
                return 0f;
            }

            float total = _goalTickPerUnit * units;
            return total <= _goalTickTotalCap ? _goalTickPerUnit : _goalTickTotalCap / units;
        }
    }
}
