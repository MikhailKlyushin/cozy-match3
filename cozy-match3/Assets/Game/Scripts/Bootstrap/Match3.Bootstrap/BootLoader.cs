using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Boot scene: shows the loading screen and hands over to Game.unity (§11.1). Kept free of
    /// gameplay so a failure here is a load failure, not a rules bug.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private int _gameSceneIndex = 1;

        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private IMatch3Logger _logger;

        [Zenject.Inject]
        public void Construct(IMatch3Logger logger) => _logger = logger;

        private void Start()
        {
            LoadAsync(_cancellation.Token).Forget(OnLoadFailed);
        }

        private void OnDestroy()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private async UniTask LoadAsync(CancellationToken ct)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(_gameSceneIndex, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    "Scene index '" + _gameSceneIndex + "' is not in the build settings.");
            }

            operation.allowSceneActivation = false;

            // LoadSceneAsync stops at 0.9 until activation is allowed.
            while (operation.progress < 0.9f)
            {
                ct.ThrowIfCancellationRequested();
                Report(operation.progress / 0.9f);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Report(1f);
            operation.allowSceneActivation = true;
        }

        private void Report(float normalized)
        {
            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(normalized);
            }

            if (_progressText != null)
            {
                _progressText.text = Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f) + "%";
            }
        }

        /// <summary>A silent Forget would turn a failed load into a frozen logo (§13).</summary>
        private void OnLoadFailed(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            if (_logger != null)
            {
                _logger.Error("Boot failed: " + exception);
                return;
            }

            Debug.LogError("[Match3] Boot failed: " + exception);
        }
    }
}
