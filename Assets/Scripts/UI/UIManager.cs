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
        [SerializeField] private GameObject textCountdownObj;
        [SerializeField] private GameObject hudGameplay;
        [SerializeField] private GameObject panelPause;
        [SerializeField] private GameObject panelEndGame;

        [Header("Dynamic Elements")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TMP_Text textCountdown;
        [SerializeField] private TMP_Text textSurvivalTimer;
        [SerializeField] private TMP_Text textEndGameTitle;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text[] soundToggleLabels;

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
            SetActiveSafe(textCountdownObj, state == GameState.Countdown);
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
            if (soundToggleLabels == null) return;
            foreach (var label in soundToggleLabels)
            {
                if (label != null)
                {
                    label.text = isMuted ? "Sound: OFF" : "Sound: ON";
                }
            }
        }

        // Hooked to UI Buttons in Inspector
        public void OnClickStart() => GameManager.Instance?.StartGame();
        public void OnClickPause() => GameManager.Instance?.TogglePause();
        public void OnClickResume() => GameManager.Instance?.TogglePause();
        public void OnClickRestart() => GameManager.Instance?.RestartGame();
        public void OnClickBackToMenu() => GameManager.Instance?.BackToMenu();
        public void OnClickToggleSound() => GameManager.Instance?.ToggleMute();

        private static void SetActiveSafe(GameObject obj, bool active)
        {
            if (obj != null && obj.activeSelf != active)
            {
                obj.SetActive(active);
            }
        }
    }
}
