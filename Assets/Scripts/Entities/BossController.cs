using System;
using System.Collections.Generic;
using UnityEngine;
using LetsDash.Core;
using LetsDash.Bullets;
using LetsDash.Combat;
using LetsDash.UI;
using LetsDash.Audio;

namespace LetsDash.Entities
{
    [Serializable]
    public struct BossStageConfig
    {
        public string stageName;
        public PathPatternType pattern;
        public BulletVisualType bulletVisual;
        public float bulletSpeed;
        public float bulletLifeTime;
        public float fireInterval;
        public float sweepAmplitudeDeg; // e.g., 10f for -10..10 deg or 180f for 360 spread
        public int projectilesPerPivot;
    }

    public class BossController : MonoBehaviour, IDamageableEntity
    {
        [Header("Identity & Health")]
        [SerializeField] private int bossID = 2;
        [SerializeField] private float maxHP = 45000f;
        [SerializeField] private float currentHP;

        [Header("Emitters & Targeting")]
        [SerializeField] private Transform pivotPointsGroup;
        [SerializeField] private List<Transform> pivotPoints = new List<Transform>();
        [SerializeField] private LayerMask leftPlayerMask;
        [SerializeField] private LayerMask rightPlayerMask;

        [Header("Stage Progression (Deterministic Modulo + Cached History)")]
        [SerializeField] private BossStageConfig[] stages;
        [SerializeField] private float stageDuration = 11f;
        [SerializeField] private int currentSeed = 7;

        [Header("Time-Scaled Pivot Balance")]
        [SerializeField] private float streamScaleIntervalSeconds = 45f;
        [SerializeField] private int maxBonusStreamsPerPivot = 6;

        private const string CacheRunCountKey = "LetsDash_ModuloCache_RunCount";
        private const string CacheLastSeedKey = "LetsDash_ModuloCache_LastSeed";
        private const string CacheRecentOpeningsKey = "LetsDash_ModuloCache_RecentOpenings";
        private const int MaxCachedOpenings = 8;

        private int currentStageIndex;
        private int stageCursor;
        private int runNumber = 1;
        private float stageTimer;
        private float fireTimer;
        private float elapsedFightTime;

        private readonly List<PathPatternType> runPatternDeck = new List<PathPatternType>();
        private readonly List<PathPatternType> activePivotPatterns = new List<PathPatternType>();
        private readonly Dictionary<PathPatternType, BossStageConfig> stageLookup = new Dictionary<PathPatternType, BossStageConfig>();

        public int EntityID => bossID;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public int RunNumber => runNumber;
        public int CurrentBonusStreams => streamScaleIntervalSeconds > 0f
            ? Mathf.Min(maxBonusStreamsPerPivot, Mathf.FloorToInt(elapsedFightTime / streamScaleIntervalSeconds))
            : 0;
        public IReadOnlyList<PathPatternType> ActivePivotPatterns => activePivotPatterns;

        private void Awake()
        {
            currentHP = maxHP;

            if (pivotPoints.Count == 0 && pivotPointsGroup != null)
            {
                for (int i = 0; i < pivotPointsGroup.childCount; i++)
                {
                    pivotPoints.Add(pivotPointsGroup.GetChild(i));
                }
            }

            EnsureDefaultStages();
            BuildStageLookup();
            InitializeCachedModuloRun();
        }

        private void Start()
        {
            HealthObserver.NotifyHealthChanged(bossID, currentHP, maxHP);
        }

        private void EnsureDefaultStages()
        {
            if (stages != null && stages.Length > 0) return;

            stages = new[]
            {
                new BossStageConfig
                {
                    stageName = "Linear Sweep",
                    pattern = PathPatternType.LinearRay,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 2.4f,
                    bulletLifeTime = 6.5f,
                    fireInterval = 0.24f,
                    sweepAmplitudeDeg = 14f,
                    projectilesPerPivot = 5
                },
                new BossStageConfig
                {
                    stageName = "Harmonic Sine",
                    pattern = PathPatternType.SineWave,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 2.1f,
                    bulletLifeTime = 7.0f,
                    fireInterval = 0.30f,
                    sweepAmplitudeDeg = 14f,
                    projectilesPerPivot = 4
                },
                new BossStageConfig
                {
                    stageName = "Archimedean Spiral",
                    pattern = PathPatternType.Spiral,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 1.75f,
                    bulletLifeTime = 7.5f,
                    fireInterval = 0.22f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 7
                },
                new BossStageConfig
                {
                    stageName = "Curved Arc Barrage",
                    pattern = PathPatternType.CurvedArc,
                    bulletVisual = BulletVisualType.Bullet_3,
                    bulletSpeed = 2.2f,
                    bulletLifeTime = 6.5f,
                    fireInterval = 0.34f,
                    sweepAmplitudeDeg = 28f,
                    projectilesPerPivot = 4
                },
                new BossStageConfig
                {
                    stageName = "Rose Mandala",
                    pattern = PathPatternType.RoseCurve,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 1.85f,
                    bulletLifeTime = 7.8f,
                    fireInterval = 0.28f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 6
                },
                new BossStageConfig
                {
                    stageName = "Logarithmic Spiral",
                    pattern = PathPatternType.LogarithmicSpiral,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 1.8f,
                    bulletLifeTime = 6.5f,
                    fireInterval = 0.26f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 6
                },
                new BossStageConfig
                {
                    stageName = "Damped Wave",
                    pattern = PathPatternType.DampedWave,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 2.2f,
                    bulletLifeTime = 6.8f,
                    fireInterval = 0.28f,
                    sweepAmplitudeDeg = 16f,
                    projectilesPerPivot = 4
                },
                new BossStageConfig
                {
                    stageName = "Expanding Helix",
                    pattern = PathPatternType.ExpandingHelix,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 2.1f,
                    bulletLifeTime = 7.0f,
                    fireInterval = 0.26f,
                    sweepAmplitudeDeg = 18f,
                    projectilesPerPivot = 4
                },
                new BossStageConfig
                {
                    stageName = "Zig-Zag Sawtooth",
                    pattern = PathPatternType.ZigZagWave,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 2.15f,
                    bulletLifeTime = 6.8f,
                    fireInterval = 0.30f,
                    sweepAmplitudeDeg = 16f,
                    projectilesPerPivot = 4
                },
                new BossStageConfig
                {
                    stageName = "Heart Cardioid Burst",
                    pattern = PathPatternType.HeartBurst,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 1.9f,
                    bulletLifeTime = 7.0f,
                    fireInterval = 0.52f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 8
                },
                new BossStageConfig
                {
                    stageName = "Starburst Polygon",
                    pattern = PathPatternType.StarburstPolygon,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 1.85f,
                    bulletLifeTime = 7.2f,
                    fireInterval = 0.48f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 8
                },
                new BossStageConfig
                {
                    stageName = "Boomerang Hook",
                    pattern = PathPatternType.BoomerangHook,
                    bulletVisual = BulletVisualType.Bullet_3,
                    bulletSpeed = 2.4f,
                    bulletLifeTime = 4.6f,
                    fireInterval = 0.38f,
                    sweepAmplitudeDeg = 22f,
                    projectilesPerPivot = 4
                }
            };
        }

        private void BuildStageLookup()
        {
            stageLookup.Clear();
            if (stages == null) return;
            for (int i = 0; i < stages.Length; i++)
            {
                if (!stageLookup.ContainsKey(stages[i].pattern))
                {
                    stageLookup[stages[i].pattern] = stages[i];
                }
            }
        }

        private List<PathPatternType> ReadAvailableSortedKeys()
        {
            if (PathFormulaLibrary.Instance != null)
            {
                return new List<PathPatternType>(PathFormulaLibrary.Instance.GetSortedPatternKeys());
            }

            // Fallback sorted keys matching PathFormulaLibrary's SortedDictionary order
            var fallback = new SortedSet<PathPatternType>();
            if (stages != null)
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    fallback.Add(stages[i].pattern);
                }
            }
            return new List<PathPatternType>(fallback);
        }

        private void InitializeCachedModuloRun()
        {
            List<PathPatternType> sortedKeys = ReadAvailableSortedKeys();
            if (sortedKeys.Count == 0) return;

            ModuloLcgRandom.EnsureInitialized();
            int cachedRunCount = PlayerPrefs.GetInt(CacheRunCountKey, 0);
            int cachedLastSeed = PlayerPrefs.GetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);
            string cachedOpeningsRaw = PlayerPrefs.GetString(CacheRecentOpeningsKey, string.Empty);

            var recentOpenings = new List<string>();
            if (!string.IsNullOrEmpty(cachedOpeningsRaw))
            {
                string[] parts = cachedOpeningsRaw.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i])) recentOpenings.Add(parts[i]);
                }
            }

            runNumber = cachedRunCount + 1;
            // Pure mathematical LCG step with zero clock/system random dependency
            ModuloLcgRandom.SetSeed(cachedLastSeed);
            int candidateSeed = ModuloLcgRandom.FeedInput(runNumber);

            int pivotCount = Mathf.Max(1, pivotPoints.Count);
            string chosenSignature = string.Empty;

            // Search deterministic LCG modulo permutations until opening avoids recent cached playthroughs
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int trySeed = attempt == 0 ? candidateSeed : ModuloLcgRandom.FeedInput(attempt + 1);
                BuildDeckWithModuloSeed(sortedKeys, trySeed);
                chosenSignature = BuildOpeningSignature(pivotCount);

                if (!recentOpenings.Contains(chosenSignature))
                {
                    candidateSeed = trySeed;
                    break;
                }
            }

            currentSeed = candidateSeed;
            ModuloLcgRandom.SetSeed(currentSeed);
            stageCursor = ModuloLcgRandom.NextMod(sortedKeys.Count);
            AssignNonDuplicatePivotPatterns();

            recentOpenings.Add(chosenSignature);
            while (recentOpenings.Count > MaxCachedOpenings)
            {
                recentOpenings.RemoveAt(0);
            }

            PlayerPrefs.SetInt(CacheRunCountKey, runNumber);
            PlayerPrefs.SetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);
            PlayerPrefs.SetString(CacheRecentOpeningsKey, string.Join(",", recentOpenings));
            PlayerPrefs.Save();
        }

        private void BuildDeckWithModuloSeed(List<PathPatternType> sortedKeys, int seed)
        {
            runPatternDeck.Clear();
            int n = sortedKeys.Count;
            if (n == 0) return;

            // Pick a stride coprime to n (for n = 12: 5, 7, 11, or 1)
            int[] coprimeCandidates = { 5, 7, 11, 1 };
            int stride = 1;
            for (int c = 0; c < coprimeCandidates.Length; c++)
            {
                int cand = coprimeCandidates[(seed + c) % coprimeCandidates.Length];
                if (GreatestCommonDivisor(cand, n) == 1)
                {
                    stride = cand;
                    break;
                }
            }

            int startOffset = ((seed % n) + n) % n;
            for (int i = 0; i < n; i++)
            {
                int idx = (startOffset + i * stride) % n;
                runPatternDeck.Add(sortedKeys[idx]);
            }

            // Deterministic modulo swap pass for extra variety across 12! permutations
            int mix = seed;
            for (int i = n - 1; i > 0; i--)
            {
                mix = Mathf.Abs((mix * 48271 + 37) % 2147483647);
                int j = mix % (i + 1);
                (runPatternDeck[i], runPatternDeck[j]) = (runPatternDeck[j], runPatternDeck[i]);
            }
        }

        private string BuildOpeningSignature(int pivotCount)
        {
            int count = Mathf.Min(pivotCount, runPatternDeck.Count);
            var ids = new string[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = ((int)runPatternDeck[i]).ToString();
            }
            return string.Join("-", ids);
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            a = Mathf.Abs(a);
            b = Mathf.Abs(b);
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a;
        }

        private void AssignNonDuplicatePivotPatterns()
        {
            activePivotPatterns.Clear();
            if (runPatternDeck.Count == 0) return;

            int deckCount = runPatternDeck.Count;
            int pivotCount = Mathf.Max(1, pivotPoints.Count);

            for (int p = 0; p < pivotCount; p++)
            {
                int idx = ((stageCursor + p) % deckCount + deckCount) % deckCount;
                activePivotPatterns.Add(runPatternDeck[idx]);
            }

            // Sync primary currentStageIndex with Pivot 0's pattern
            if (activePivotPatterns.Count > 0 && stages != null)
            {
                PathPatternType leadPattern = activePivotPatterns[0];
                for (int i = 0; i < stages.Length; i++)
                {
                    if (stages[i].pattern == leadPattern)
                    {
                        currentStageIndex = i;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            float dt = Time.deltaTime;
            elapsedFightTime += dt;
            stageTimer += dt;
            fireTimer += dt;

            if (stageTimer >= stageDuration)
            {
                stageTimer = 0f;
                SelectNextStageModulo();
            }

            if (stages == null || stages.Length == 0) return;

            BossStageConfig activeStage = stages[currentStageIndex];
            if (fireTimer >= activeStage.fireInterval)
            {
                fireTimer = 0f;
                ExecuteFirePattern(activeStage);
            }
        }

        public void SelectNextStageModulo()
        {
            if (runPatternDeck.Count == 0)
            {
                InitializeCachedModuloRun();
            }

            int deckCount = runPatternDeck.Count;
            if (deckCount == 0) return;

            // Pure mathematical LCG Result = Seed_new % 12 (influenced by W/A/S/D keystrokes)
            int lcgIndex = ModuloLcgRandom.NextMod(deckCount);
            if (lcgIndex == stageCursor)
            {
                int pivotCount = Mathf.Max(1, pivotPoints.Count);
                int fallbackStep = 1;
                for (int candidate = pivotCount; candidate < deckCount + pivotCount; candidate++)
                {
                    if (GreatestCommonDivisor(candidate, deckCount) == 1)
                    {
                        fallbackStep = candidate;
                        break;
                    }
                }
                lcgIndex = (stageCursor + fallbackStep) % deckCount;
            }

            currentSeed = ModuloLcgRandom.CurrentSeed;
            stageCursor = lcgIndex;

            PlayerPrefs.SetInt(CacheLastSeedKey, currentSeed);
            ModuloLcgRandom.SaveSeed();

            AssignNonDuplicatePivotPatterns();
        }

        private void OnDisable()
        {
            if (ModuloLcgRandom.CurrentSeed > 0)
            {
                PlayerPrefs.SetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);
                ModuloLcgRandom.SaveSeed();
            }
        }

        public void ExecuteFirePattern(BossStageConfig fallbackStage)
        {
            if (BulletPoolManager.Instance == null || pivotPoints.Count == 0) return;

            int bonusStreams = CurrentBonusStreams;

            for (int p = 0; p < pivotPoints.Count; p++)
            {
                Transform pivot = pivotPoints[p];
                if (pivot == null) continue;

                PathPatternType pivotPattern = p < activePivotPatterns.Count
                    ? activePivotPatterns[p]
                    : fallbackStage.pattern;

                BossStageConfig pivotStage = stageLookup.TryGetValue(pivotPattern, out var cfg)
                    ? cfg
                    : fallbackStage;

                // Left pivots target Left player mask; Right pivots target Right player mask
                bool isLeftSide = pivot.position.x < 0f;
                LayerMask targetMask = isLeftSide ? leftPlayerMask : rightPlayerMask;

                float sweep = Mathf.Sin(elapsedFightTime * 2.4f + p * 0.65f) * pivotStage.sweepAmplitudeDeg;
                int count = Mathf.Max(1, pivotStage.projectilesPerPivot + bonusStreams);

                for (int i = 0; i < count; i++)
                {
                    float spreadOffset = 0f;
                    if (count > 1)
                    {
                        float t = (float)i / (count - 1) - 0.5f;
                        spreadOffset = t * (pivotStage.sweepAmplitudeDeg * 2f);
                    }

                    // -90 deg points straight downward
                    float angleA = -90f + sweep + spreadOffset;

                    BulletPoolManager.Instance.SpawnBullet(
                        pivotStage.bulletVisual,
                        pivotPattern,
                        pivot.position,
                        angleA,
                        pivotStage.bulletSpeed,
                        pivotStage.bulletLifeTime,
                        15f,
                        targetMask,
                        bossID
                    );
                }
            }

            AudioAndVFXManager.Instance?.PlayBossFireSFX();
        }

        public void TakeDamage(float amount)
        {
            if (currentHP <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            HealthObserver.NotifyHealthChanged(bossID, currentHP, maxHP);

            if (currentHP <= 0f)
            {
                AudioAndVFXManager.Instance?.SpawnExplosionVFX(transform.position);
                GameManager.Instance?.OnEntityDied(bossID);
            }
        }
    }
}
