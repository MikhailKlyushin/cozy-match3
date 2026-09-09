using System;
using Match3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Cheats
{
    /// <summary>
    /// Widgets of the cheat panel (§12). uGUI buttons, toggles and input fields only: the panel is
    /// on-screen controls by client requirement, and there is not a single hot key anywhere.
    /// Display and events only - every decision belongs to <see cref="CheatPanelPresenter"/>.
    /// </summary>
    public sealed class CheatPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _window;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Button _closeButton;

        [SerializeField] private InputField _levelInput;
        [SerializeField] private Button _goToLevelButton;
        [SerializeField] private Text _levelStatusLabel;

        [SerializeField] private Button _winLevelButton;
        [SerializeField] private Button _loseLevelButton;
        [SerializeField] private Button _addMovesButton;

        [SerializeField] private Button _rocketBoosterButton;
        [SerializeField] private Button _bombBoosterButton;
        [SerializeField] private Button _rainbowBoosterButton;
        [SerializeField] private Button _airplaneBoosterButton;
        [SerializeField] private Button _disarmBoosterButton;
        [SerializeField] private Text _armedBoosterLabel;
        [SerializeField] private Image _armedBoosterIcon;

        [SerializeField] private Text _seedLabel;
        [SerializeField] private InputField _seedInput;
        [SerializeField] private Button _applySeedButton;
        [SerializeField] private Button _restartAttemptButton;

        [SerializeField] private Toggle _freeMovesToggle;
        [SerializeField] private Toggle _coordinateGridToggle;
        [SerializeField] private Toggle _disableHintsToggle;

        [SerializeField] private Button _hintNowButton;
        [SerializeField] private Button _dumpTranscriptButton;

        public event Action CloseClicked;

        public event Action<string> LevelNumberChanged;

        public event Action GoToLevelClicked;

        public event Action WinLevelClicked;

        public event Action LoseLevelClicked;

        public event Action AddMovesClicked;

        public event Action<BoosterType> BoosterSelected;

        public event Action DisarmBoosterClicked;

        public event Action ApplySeedClicked;

        public event Action RestartAttemptClicked;

        public event Action<bool> FreeMovesChanged;

        public event Action<bool> CoordinateGridChanged;

        public event Action<bool> DisableHintsChanged;

        public event Action HintNowClicked;

        public event Action DumpTranscriptClicked;

        public bool IsVisible => _window != null && _window.activeSelf;

        public string LevelText => _levelInput != null ? _levelInput.text : string.Empty;

        public string SeedText => _seedInput != null ? _seedInput.text : string.Empty;

        public void SetVisible(bool visible)
        {
            if (_window != null)
            {
                _window.SetActive(visible);
            }
        }

        public void SetLevelStatus(string text)
        {
            if (_levelStatusLabel != null)
            {
                _levelStatusLabel.text = text;
            }
        }

        public void SetGoToLevelInteractable(bool value) => SetInteractable(_goToLevelButton, value);

        public void SetSeedDisplay(string text)
        {
            if (_seedLabel != null)
            {
                _seedLabel.text = text;
            }
        }

        /// <summary>Fills the seed field without firing the change event, so "Заново" is one press.</summary>
        public void SetSeedText(string text)
        {
            if (_seedInput != null)
            {
                _seedInput.SetTextWithoutNotify(text);
            }
        }

        public void SetArmedBooster(BoosterType booster, Sprite icon)
        {
            if (_armedBoosterLabel != null)
            {
                _armedBoosterLabel.text = booster == BoosterType.None
                    ? CheatStrings.BoosterNotArmed
                    : string.Format(CheatStrings.BoosterArmedFormat, CheatStrings.BoosterName(booster));
            }

            if (_armedBoosterIcon != null)
            {
                _armedBoosterIcon.sprite = icon;
                _armedBoosterIcon.enabled = icon != null;
            }

            SetInteractable(_disarmBoosterButton, booster != BoosterType.None);
        }

        public void SetFreeMoves(bool value) => SetToggle(_freeMovesToggle, value);

        public void SetCoordinateGrid(bool value) => SetToggle(_coordinateGridToggle, value);

        public void SetDisableHints(bool value) => SetToggle(_disableHintsToggle, value);

        /// <summary>Everything that needs a live attempt and an idle board (§5.1 IDLE).</summary>
        public void SetBoardCheatsInteractable(bool value)
        {
            SetInteractable(_winLevelButton, value);
            SetInteractable(_loseLevelButton, value);
            SetInteractable(_addMovesButton, value);
            SetInteractable(_rocketBoosterButton, value);
            SetInteractable(_bombBoosterButton, value);
            SetInteractable(_rainbowBoosterButton, value);
            SetInteractable(_airplaneBoosterButton, value);
            SetInteractable(_hintNowButton, value);
            SetToggleInteractable(_freeMovesToggle, value);
            SetToggleInteractable(_disableHintsToggle, value);
        }

        public void SetSeedActionsInteractable(bool applyEnabled, bool restartEnabled)
        {
            SetInteractable(_applySeedButton, applyEnabled);
            SetInteractable(_restartAttemptButton, restartEnabled);
        }

        public void SetDumpInteractable(bool value) => SetInteractable(_dumpTranscriptButton, value);

        public void SetCoordinateGridInteractable(bool value) => SetToggleInteractable(_coordinateGridToggle, value);

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        private static void SetToggleInteractable(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.interactable = value;
            }
        }

        private static void SetToggle(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        /// <summary>
        /// Captions come from <see cref="CheatStrings"/> rather than the prefab, so the Russian
        /// §12 wording is reviewable in code and cannot drift when the prefab is regenerated.
        /// </summary>
        private static void SetCaption(Component target, string caption)
        {
            if (target == null)
            {
                return;
            }

            Text label = target.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = caption;
            }
        }

        private static void SetPlaceholder(InputField field, string caption)
        {
            if (field == null)
            {
                return;
            }

            field.contentType = InputField.ContentType.IntegerNumber;

            if (field.placeholder is Text placeholder)
            {
                placeholder.text = caption;
            }
        }

        private void Awake()
        {
            ApplyCaptions();
            AddListeners();
        }

        private void OnDestroy()
        {
            RemoveListeners();

            CloseClicked = null;
            LevelNumberChanged = null;
            GoToLevelClicked = null;
            WinLevelClicked = null;
            LoseLevelClicked = null;
            AddMovesClicked = null;
            BoosterSelected = null;
            DisarmBoosterClicked = null;
            ApplySeedClicked = null;
            RestartAttemptClicked = null;
            FreeMovesChanged = null;
            CoordinateGridChanged = null;
            DisableHintsChanged = null;
            HintNowClicked = null;
            DumpTranscriptClicked = null;
        }

        private void ApplyCaptions()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = CheatStrings.Title;
            }

            SetCaption(_closeButton, CheatStrings.Close);
            SetCaption(_goToLevelButton, CheatStrings.GoToLevel);
            SetCaption(_winLevelButton, CheatStrings.WinLevel);
            SetCaption(_loseLevelButton, CheatStrings.LoseLevel);
            SetCaption(_addMovesButton, CheatStrings.AddMoves);
            SetCaption(_rocketBoosterButton, CheatStrings.RocketBooster);
            SetCaption(_bombBoosterButton, CheatStrings.BombBooster);
            SetCaption(_rainbowBoosterButton, CheatStrings.RainbowBooster);
            SetCaption(_airplaneBoosterButton, CheatStrings.AirplaneBooster);
            SetCaption(_disarmBoosterButton, CheatStrings.DisarmBooster);
            SetCaption(_applySeedButton, CheatStrings.ApplySeed);
            SetCaption(_restartAttemptButton, CheatStrings.RestartAttempt);
            SetCaption(_freeMovesToggle, CheatStrings.FreeMoves);
            SetCaption(_coordinateGridToggle, CheatStrings.CoordinateGrid);
            SetCaption(_disableHintsToggle, CheatStrings.DisableHints);
            SetCaption(_hintNowButton, CheatStrings.HintNow);
            SetCaption(_dumpTranscriptButton, CheatStrings.DumpTranscript);

            SetPlaceholder(_levelInput, CheatStrings.LevelPlaceholder);
            SetPlaceholder(_seedInput, CheatStrings.SeedPlaceholder);
        }

        private void AddListeners()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (_levelInput != null)
            {
                _levelInput.onValueChanged.AddListener(OnLevelNumberChanged);
            }

            if (_goToLevelButton != null)
            {
                _goToLevelButton.onClick.AddListener(OnGoToLevelClicked);
            }

            if (_winLevelButton != null)
            {
                _winLevelButton.onClick.AddListener(OnWinLevelClicked);
            }

            if (_loseLevelButton != null)
            {
                _loseLevelButton.onClick.AddListener(OnLoseLevelClicked);
            }

            if (_addMovesButton != null)
            {
                _addMovesButton.onClick.AddListener(OnAddMovesClicked);
            }

            if (_rocketBoosterButton != null)
            {
                _rocketBoosterButton.onClick.AddListener(OnRocketSelected);
            }

            if (_bombBoosterButton != null)
            {
                _bombBoosterButton.onClick.AddListener(OnBombSelected);
            }

            if (_rainbowBoosterButton != null)
            {
                _rainbowBoosterButton.onClick.AddListener(OnRainbowSelected);
            }

            if (_airplaneBoosterButton != null)
            {
                _airplaneBoosterButton.onClick.AddListener(OnAirplaneSelected);
            }

            if (_disarmBoosterButton != null)
            {
                _disarmBoosterButton.onClick.AddListener(OnDisarmBoosterClicked);
            }

            if (_applySeedButton != null)
            {
                _applySeedButton.onClick.AddListener(OnApplySeedClicked);
            }

            if (_restartAttemptButton != null)
            {
                _restartAttemptButton.onClick.AddListener(OnRestartAttemptClicked);
            }

            if (_freeMovesToggle != null)
            {
                _freeMovesToggle.onValueChanged.AddListener(OnFreeMovesChanged);
            }

            if (_coordinateGridToggle != null)
            {
                _coordinateGridToggle.onValueChanged.AddListener(OnCoordinateGridChanged);
            }

            if (_disableHintsToggle != null)
            {
                _disableHintsToggle.onValueChanged.AddListener(OnDisableHintsChanged);
            }

            if (_hintNowButton != null)
            {
                _hintNowButton.onClick.AddListener(OnHintNowClicked);
            }

            if (_dumpTranscriptButton != null)
            {
                _dumpTranscriptButton.onClick.AddListener(OnDumpTranscriptClicked);
            }
        }

        private void RemoveListeners()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (_levelInput != null)
            {
                _levelInput.onValueChanged.RemoveListener(OnLevelNumberChanged);
            }

            if (_goToLevelButton != null)
            {
                _goToLevelButton.onClick.RemoveListener(OnGoToLevelClicked);
            }

            if (_winLevelButton != null)
            {
                _winLevelButton.onClick.RemoveListener(OnWinLevelClicked);
            }

            if (_loseLevelButton != null)
            {
                _loseLevelButton.onClick.RemoveListener(OnLoseLevelClicked);
            }

            if (_addMovesButton != null)
            {
                _addMovesButton.onClick.RemoveListener(OnAddMovesClicked);
            }

            if (_rocketBoosterButton != null)
            {
                _rocketBoosterButton.onClick.RemoveListener(OnRocketSelected);
            }

            if (_bombBoosterButton != null)
            {
                _bombBoosterButton.onClick.RemoveListener(OnBombSelected);
            }

            if (_rainbowBoosterButton != null)
            {
                _rainbowBoosterButton.onClick.RemoveListener(OnRainbowSelected);
            }

            if (_airplaneBoosterButton != null)
            {
                _airplaneBoosterButton.onClick.RemoveListener(OnAirplaneSelected);
            }

            if (_disarmBoosterButton != null)
            {
                _disarmBoosterButton.onClick.RemoveListener(OnDisarmBoosterClicked);
            }

            if (_applySeedButton != null)
            {
                _applySeedButton.onClick.RemoveListener(OnApplySeedClicked);
            }

            if (_restartAttemptButton != null)
            {
                _restartAttemptButton.onClick.RemoveListener(OnRestartAttemptClicked);
            }

            if (_freeMovesToggle != null)
            {
                _freeMovesToggle.onValueChanged.RemoveListener(OnFreeMovesChanged);
            }

            if (_coordinateGridToggle != null)
            {
                _coordinateGridToggle.onValueChanged.RemoveListener(OnCoordinateGridChanged);
            }

            if (_disableHintsToggle != null)
            {
                _disableHintsToggle.onValueChanged.RemoveListener(OnDisableHintsChanged);
            }

            if (_hintNowButton != null)
            {
                _hintNowButton.onClick.RemoveListener(OnHintNowClicked);
            }

            if (_dumpTranscriptButton != null)
            {
                _dumpTranscriptButton.onClick.RemoveListener(OnDumpTranscriptClicked);
            }
        }

        private void OnCloseClicked() => CloseClicked?.Invoke();

        private void OnLevelNumberChanged(string text) => LevelNumberChanged?.Invoke(text);

        private void OnGoToLevelClicked() => GoToLevelClicked?.Invoke();

        private void OnWinLevelClicked() => WinLevelClicked?.Invoke();

        private void OnLoseLevelClicked() => LoseLevelClicked?.Invoke();

        private void OnAddMovesClicked() => AddMovesClicked?.Invoke();

        private void OnRocketSelected() => BoosterSelected?.Invoke(BoosterType.RocketH);

        private void OnBombSelected() => BoosterSelected?.Invoke(BoosterType.Bomb);

        private void OnRainbowSelected() => BoosterSelected?.Invoke(BoosterType.Rainbow);

        private void OnAirplaneSelected() => BoosterSelected?.Invoke(BoosterType.Airplane);

        private void OnDisarmBoosterClicked() => DisarmBoosterClicked?.Invoke();

        private void OnApplySeedClicked() => ApplySeedClicked?.Invoke();

        private void OnRestartAttemptClicked() => RestartAttemptClicked?.Invoke();

        private void OnFreeMovesChanged(bool value) => FreeMovesChanged?.Invoke(value);

        private void OnCoordinateGridChanged(bool value) => CoordinateGridChanged?.Invoke(value);

        private void OnDisableHintsChanged(bool value) => DisableHintsChanged?.Invoke(value);

        private void OnHintNowClicked() => HintNowClicked?.Invoke();

        private void OnDumpTranscriptClicked() => DumpTranscriptClicked?.Invoke();
    }
}
