using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LetsDash.Bullets;
using LetsDash.Audio;

namespace LetsDash.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State & Timers")]
        [SerializeField] private GameState currentState = GameState.Menu;
        [SerializeField] private float totalSurvivalDuration = 480f; // 8 minutes
        [SerializeField] private float countdownDuration = 5f;

        private float survivalTimeRemaining;
        private float countdownTimer;
        private bool isMuted;
        private bool isEndingGame;

        public GameState CurrentState => currentState;
        public float SurvivalTimeRemaining => survivalTimeRemaining;
        public float ElapsedPlayTime => Mathf.Max(0f, totalSurvivalDuration - survivalTimeRemaining);
        public float CountdownTimer => countdownTimer;
        public bool IsMuted => isMuted;
        public bool IsPauseLocked => currentState == GameState.Countdown || currentState == GameState.Loading || currentState == GameState.Menu || isEndingGame;

        public static event Action<GameState> OnGameStateChanged;
        public static event Action<float> OnLoadingProgressChanged;
        public static event Action<float> OnCountdownTick;
        public static event Action<float> OnSurvivalTimerTick;
        public static event Action<bool> OnMuteChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            survivalTimeRemaining = totalSurvivalDuration;
            countdownTimer = countdownDuration;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = isMuted;
            ChangeState(GameState.Menu);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }

            UpdateTimers();
        }

        public void StartGame()
        {
            if (currentState != GameState.Menu) return;
            StartCoroutine(StartLoadingSequence());
        }

        public void ChangeState(GameState newState)
        {
            currentState = newState;

            switch (currentState)
            {
                case GameState.Menu:
                    isEndingGame = false;
                    Time.timeScale = 1f;
                    break;
                case GameState.Loading:
                    isEndingGame = false;
                    Time.timeScale = 1f;
                    break;
                case GameState.Countdown:
                    isEndingGame = false;
                    Time.timeScale = 1f;
                    countdownTimer = countdownDuration;
                    survivalTimeRemaining = totalSurvivalDuration;
                    OnCountdownTick?.Invoke(countdownTimer);
                    OnSurvivalTimerTick?.Invoke(survivalTimeRemaining);
                    break;
                case GameState.Playing:
                    isEndingGame = false;
                    Time.timeScale = 1f;
                    AudioAndVFXManager.Instance?.PlayBGM();
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    Time.timeScale = 0f;
                    AudioAndVFXManager.Instance?.StopBGM();
                    break;
            }

            OnGameStateChanged?.Invoke(currentState);
        }

        private IEnumerator StartLoadingSequence()
        {
            ChangeState(GameState.Loading);
            OnLoadingProgressChanged?.Invoke(0f);

            // Step 1: Prewarm centralized bullet pool
            if (BulletPoolManager.Instance != null)
            {
                yield return StartCoroutine(BulletPoolManager.Instance.PrewarmPoolCoroutine(progress =>
                {
                    OnLoadingProgressChanged?.Invoke(progress * 0.7f);
                }));
            }
            else
            {
                OnLoadingProgressChanged?.Invoke(0.7f);
            }

            // Step 2: Prewarm VFX pool
            if (AudioAndVFXManager.Instance != null)
            {
                AudioAndVFXManager.Instance.PrewarmVFXPools();
            }
            OnLoadingProgressChanged?.Invoke(1f);

            yield return new WaitForSecondsRealtime(0.15f);
            ChangeState(GameState.Countdown);
        }

        private void UpdateTimers()
        {
            if (currentState == GameState.Countdown)
            {
                countdownTimer -= Time.deltaTime;
                OnCountdownTick?.Invoke(Mathf.Max(0f, countdownTimer));

                if (countdownTimer <= 0f)
                {
                    ChangeState(GameState.Playing);
                }
            }
            else if (currentState == GameState.Playing && !isEndingGame)
            {
                survivalTimeRemaining -= Time.deltaTime;
                OnSurvivalTimerTick?.Invoke(Mathf.Max(0f, survivalTimeRemaining));

                if (survivalTimeRemaining <= 0f)
                {
                    survivalTimeRemaining = 0f;
                    ChangeState(GameState.Victory);
                }
            }
        }

        public void TogglePause()
        {
            if (isEndingGame) return;

            // Pause is explicitly locked during Countdown, Loading, Menu, and End states
            if (currentState == GameState.Playing)
            {
                ChangeState(GameState.Paused);
            }
            else if (currentState == GameState.Paused)
            {
                ChangeState(GameState.Playing);
            }
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            AudioListener.pause = isMuted;
            OnMuteChanged?.Invoke(isMuted);
        }

        public void RestartGame()
        {
            isEndingGame = false;
            Time.timeScale = 1f;
            BulletPoolManager.Instance?.DeactivateAllBullets();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void BackToMenu()
        {
            isEndingGame = false;
            Time.timeScale = 1f;
            BulletPoolManager.Instance?.DeactivateAllBullets();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void OnEntityDied(int entityID)
        {
            if (currentState != GameState.Playing || isEndingGame) return;

            // Entity 0 = Left Player, Entity 1 = Right Player, Entity 2 = Boss
            if (entityID == 0 || entityID == 1)
            {
                isEndingGame = true;
                StartCoroutine(TriggerDelayedGameOverCoroutine());
            }
            else if (entityID == 2)
            {
                isEndingGame = true;
                ChangeState(GameState.Victory);
            }
        }

        private IEnumerator TriggerDelayedGameOverCoroutine()
        {
            yield return new WaitForSecondsRealtime(1.0f);
            ChangeState(GameState.GameOver);
        }
    }
}
