using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LetsDash.Core;

namespace LetsDash.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject panelMenu;
        [SerializeField] private GameObject panelLoading;
        [SerializeField] private GameObject hudGameplay;
        [SerializeField] private GameObject panelPause;
        [SerializeField] private GameObject panelEndGame;

        [Header("Dynamic Elements")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TMP_Text textCountdown;
        [SerializeField] private TMP_Text textSurvivalTimer;
        [SerializeField] private TMP_Text textEndGameTitle;

        [Header("Buttons (Auto-Wired in Awake)")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button[] restartButtons;
        [SerializeField] private Button[] backToMenuButtons;
        [SerializeField] private Button[] soundToggleButtons;

        [Header("Sound Icon Sprites")]
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(() => GameManager.Instance?.StartGame());
            }

            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(() => GameManager.Instance?.TogglePause());
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(() => GameManager.Instance?.TogglePause());
            }

            BindButtonArray(restartButtons, () => GameManager.Instance?.RestartGame());
            BindButtonArray(backToMenuButtons, () => GameManager.Instance?.BackToMenu());
            BindButtonArray(soundToggleButtons, () => GameManager.Instance?.ToggleMute());
        }

        private void Start()
        {
            bool isMuted = GameManager.Instance != null ? GameManager.Instance.IsMuted : AudioListener.pause;
            HandleMuteChanged(isMuted);
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
            GameManager.OnLoadingProgressChanged += HandleLoadingProgress;
            GameManager.OnCountdownTick += HandleCountdownTick;
            GameManager.OnSurvivalTimerTick += HandleSurvivalTimerTick;
            GameManager.OnMuteChanged += HandleMuteChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.OnLoadingProgressChanged -= HandleLoadingProgress;
            GameManager.OnCountdownTick -= HandleCountdownTick;
            GameManager.OnSurvivalTimerTick -= HandleSurvivalTimerTick;
            GameManager.OnMuteChanged -= HandleMuteChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            SetActiveSafe(panelMenu, state == GameState.Menu);
            SetActiveSafe(panelLoading, state == GameState.Loading);
            if (textCountdown != null)
            {
                SetActiveSafe(textCountdown.gameObject, state == GameState.Countdown);
            }
            SetActiveSafe(hudGameplay, state == GameState.Countdown || state == GameState.Playing || state == GameState.Paused);
            SetActiveSafe(panelPause, state == GameState.Paused);
            SetActiveSafe(panelEndGame, state == GameState.GameOver || state == GameState.Victory);

            if (pauseButton != null)
            {
                // Pause button is explicitly locked during Countdown
                pauseButton.interactable = (state == GameState.Playing || state == GameState.Paused);
            }

            if (textEndGameTitle != null)
            {
                if (state == GameState.Victory)
                {
                    textEndGameTitle.text = "VICTORY! PARALLEL SURVIVAL COMPLETE";
                }
                else if (state == GameState.GameOver)
                {
                    textEndGameTitle.text = "GAME OVER — PARALLEL LINK BROKEN";
                }
            }
        }

        private void HandleLoadingProgress(float progress)
        {
            if (loadingProgressBar != null)
            {
                loadingProgressBar.value = Mathf.Clamp01(progress);
            }
        }

        private void HandleCountdownTick(float remaining)
        {
            if (textCountdown != null)
            {
                int ceilSeconds = Mathf.CeilToInt(remaining);
                textCountdown.text = ceilSeconds > 0 ? ceilSeconds.ToString() : "GO!";
            }
        }

        private void HandleSurvivalTimerTick(float remaining)
        {
            if (textSurvivalTimer == null) return;

            int totalSeconds = Mathf.CeilToInt(remaining);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            textSurvivalTimer.text = $"{minutes:00}:{seconds:00}";
        }

        private void HandleMuteChanged(bool isMuted)
        {
            if (soundToggleButtons == null) return;

            Sprite targetSprite = isMuted ? soundOffSprite : soundOnSprite;
            if (targetSprite == null) return;

            for (int i = 0; i < soundToggleButtons.Length; i++)
            {
                Button btn = soundToggleButtons[i];
                if (btn == null) continue;

                Image iconImage = btn.image != null ? btn.image : btn.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = targetSprite;
                }
            }
        }

        private static void BindButtonArray(Button[] buttons, UnityEngine.Events.UnityAction action)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].onClick.AddListener(action);
                }
            }
        }

        private static void SetActiveSafe(GameObject obj, bool active)
        {
            if (obj != null && obj.activeSelf != active)
            {
                obj.SetActive(active);
            }
        }
    }
}
