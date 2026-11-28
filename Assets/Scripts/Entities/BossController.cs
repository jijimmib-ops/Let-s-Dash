using System;
using System.Collections;
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
    public class PivotFireConfig
    {
        public Transform pivotTransform;
        public List<BulletPathType> allowedPatterns = new List<BulletPathType>();
    }

    [Serializable]
    public struct BossPatternConfig
    {
        public string patternName;
        public BulletPathType pattern;
        public BulletVisualType bulletVisual;
        public float bulletSpeed;
        public float bulletLifeTime;
        public float sweepAmplitudeDeg;
        public int projectilesPerPivot;
    }

    public class BossController : MonoBehaviour, IDamageableEntity
    {
        [Header("Identity & Health")]
        [SerializeField] private int bossID = 2;
        [SerializeField] private float maxHP = 45000f;
        [SerializeField] private float currentHP;

        [Header("Per-Pivot Emitters & Targeting")]
        [SerializeField] private Transform pivotPointsGroup;
        [SerializeField] private List<PivotFireConfig> pivotConfigs = new List<PivotFireConfig>();
        [SerializeField] private LayerMask playerTargetMask;

        [Header("Dynamic Fire Rate Scaling")]
        [SerializeField, Min(0.01f)] private float maxFireInterval = 1.5f;
        [SerializeField, Min(0.01f)] private float minFireInterval = 0.01f;
        [SerializeField, Min(0.1f)] private float rampUpDuration = 480f;

        [Header("Pattern Profiles & Deterministic Seed")]
        [SerializeField] private BossPatternConfig[] patternConfigs;
        [SerializeField] private int currentSeed = 7;

        [Header("Time-Scaled Pivot Balance")]
        [SerializeField] private float streamScaleIntervalSeconds = 45f;
        [SerializeField] private int maxBonusStreamsPerPivot = 6;

        [Header("Damage Flash")]
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private float damageFlashDuration = 0.1f;

        private const string CacheRunCountKey = "LetsDash_ModuloCache_RunCount";
        private const string CacheLastSeedKey = "LetsDash_ModuloCache_LastSeed";

        private int runNumber = 1;
        private float fireTimer;
        private float elapsedFightTime;
        private SpriteRenderer[] cachedSpriteRenderers;
        private Color[] originalSpriteColors;
        private Coroutine damageFlashCoroutine;

        private readonly Dictionary<BulletPathType, BossPatternConfig> patternLookup = new Dictionary<BulletPathType, BossPatternConfig>();
        private readonly List<BulletPathType> lastRolledPivotPatterns = new List<BulletPathType>();

        public int EntityID => bossID;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public int RunNumber => runNumber;
        public int CurrentBonusStreams => streamScaleIntervalSeconds > 0f
            ? Mathf.Min(maxBonusStreamsPerPivot, Mathf.FloorToInt(elapsedFightTime / streamScaleIntervalSeconds))
            : 0;
        public float CurrentFireInterval => GetCurrentFireInterval();
        public IReadOnlyList<PivotFireConfig> PivotConfigs => pivotConfigs;
        public IReadOnlyList<BulletPathType> LastRolledPivotPatterns => lastRolledPivotPatterns;

        private void OnValidate()
        {
            minFireInterval = Mathf.Max(0.01f, minFireInterval);
            maxFireInterval = Mathf.Max(minFireInterval, maxFireInterval);
            rampUpDuration = Mathf.Max(0.1f, rampUpDuration);
        }

        private void Awake()
        {
            currentHP = maxHP;

            cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalSpriteColors = new Color[cachedSpriteRenderers.Length];
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                originalSpriteColors[i] = cachedSpriteRenderers[i].color;
            }

            EnsurePivotConfigs();
            EnsureDefaultPatternConfigs();
            BuildPatternLookup();
            InitializeDeterministicModuloRun();
        }

        private void Start()
        {
            HealthObserver.NotifyHealthChanged(bossID, currentHP, maxHP);
        }

        private void EnsurePivotConfigs()
        {
            if (pivotConfigs == null)
            {
                pivotConfigs = new List<PivotFireConfig>();
            }

            if (pivotConfigs.Count == 0 && pivotPointsGroup != null)
            {
                for (int i = 0; i < pivotPointsGroup.childCount; i++)
                {
                    pivotConfigs.Add(new PivotFireConfig
                    {
                        pivotTransform = pivotPointsGroup.GetChild(i),
                        allowedPatterns = CreateDefaultAllowedPatterns()
                    });
                }
            }
        }

        private static List<BulletPathType> CreateDefaultAllowedPatterns()
        {
            return new List<BulletPathType>
            {
                BulletPathType.LinearRay,
                BulletPathType.HeartCardioidBurst,
                BulletPathType.CurvedArc,
                BulletPathType.ArchimedeanSpiral,
                BulletPathType.Space
            };
        }

        private void EnsureDefaultPatternConfigs()
        {
            if (patternConfigs != null && patternConfigs.Length > 0) return;

            patternConfigs = new[]
            {
                new BossPatternConfig
                {
                    patternName = "Linear Ray",
                    pattern = BulletPathType.LinearRay,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 2.6f,
                    bulletLifeTime = 6.5f,
                    sweepAmplitudeDeg = 16f,
                    projectilesPerPivot = 5
                },
                new BossPatternConfig
                {
                    patternName = "Heart Cardioid Burst",
                    pattern = BulletPathType.HeartCardioidBurst,
                    bulletVisual = BulletVisualType.Bullet_2,
                    bulletSpeed = 2.2f,
                    bulletLifeTime = 6.5f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 8
                },
                new BossPatternConfig
                {
                    patternName = "Curved Arc",
                    pattern = BulletPathType.CurvedArc,
                    bulletVisual = BulletVisualType.Bullet_3,
                    bulletSpeed = 2.4f,
                    bulletLifeTime = 6.5f,
                    sweepAmplitudeDeg = 28f,
                    projectilesPerPivot = 4
                },
                new BossPatternConfig
                {
                    patternName = "Archimedean Spiral",
                    pattern = BulletPathType.ArchimedeanSpiral,
                    bulletVisual = BulletVisualType.Bullet_1,
                    bulletSpeed = 2.0f,
                    bulletLifeTime = 7.0f,
                    sweepAmplitudeDeg = 180f,
                    projectilesPerPivot = 6
                }
            };
        }

        private void BuildPatternLookup()
        {
            patternLookup.Clear();
            if (patternConfigs == null) return;

            for (int i = 0; i < patternConfigs.Length; i++)
            {
                if (!patternLookup.ContainsKey(patternConfigs[i].pattern))
                {
                    patternLookup[patternConfigs[i].pattern] = patternConfigs[i];
                }
            }
        }

        private void InitializeDeterministicModuloRun()
        {
            ModuloLcgRandom.EnsureInitialized();
            int cachedRunCount = PlayerPrefs.GetInt(CacheRunCountKey, 0);
            int cachedLastSeed = PlayerPrefs.GetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);

            runNumber = cachedRunCount + 1;
            ModuloLcgRandom.SetSeed(cachedLastSeed);
            currentSeed = ModuloLcgRandom.FeedInput(runNumber);

            PlayerPrefs.SetInt(CacheRunCountKey, runNumber);
            PlayerPrefs.SetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            float dt = Time.deltaTime;
            elapsedFightTime += dt;
            fireTimer += dt;

            float currentFireInterval = GetCurrentFireInterval();
            if (fireTimer >= currentFireInterval)
            {
                fireTimer = 0f;
                ExecuteFirePattern();
            }
        }

        private float GetCurrentFireInterval()
        {
            float elapsedPlayTime = GameManager.Instance != null
                ? GameManager.Instance.ElapsedPlayTime
                : elapsedFightTime;

            float progress = rampUpDuration > 0f ? Mathf.Clamp01(elapsedPlayTime / rampUpDuration) : 1f;
            float safeMin = Mathf.Max(0.01f, minFireInterval);
            float safeMax = Mathf.Max(safeMin, maxFireInterval);
            return Mathf.Max(0.01f, Mathf.Lerp(safeMax, safeMin, progress));
        }

        public void ExecuteFirePattern()
        {
            if (BulletPoolManager.Instance == null || pivotConfigs == null || pivotConfigs.Count == 0) return;

            int bonusStreams = CurrentBonusStreams;
            bool anyBulletFired = false;
            lastRolledPivotPatterns.Clear();

            for (int p = 0; p < pivotConfigs.Count; p++)
            {
                PivotFireConfig config = pivotConfigs[p];
                if (config == null || config.pivotTransform == null || config.allowedPatterns == null || config.allowedPatterns.Count == 0)
                {
                    continue;
                }

                // Roll strictly within [0, allowedPatterns.Count - 1] using deterministic ModuloLcgRandom
                int rolledIndex = ModuloLcgRandom.NextMod(config.allowedPatterns.Count);
                BulletPathType selectedType = config.allowedPatterns[rolledIndex];
                lastRolledPivotPatterns.Add(selectedType);

                // If Space is rolled, skip spawning bullets for this pivot on this firing cycle
                bool isSpace = PathFormulaLibrary.Instance != null
                    ? PathFormulaLibrary.Instance.IsEmptySpace(selectedType)
                    : selectedType == BulletPathType.Space;
                if (isSpace)
                {
                    continue;
                }

                // Verify formula exists in PathFormulaLibrary's SortedDictionary
                if (PathFormulaLibrary.Instance != null &&
                    !PathFormulaLibrary.Instance.TryGetFormula(selectedType, out _))
                {
                    continue;
                }

                BossPatternConfig cfg = GetConfigForPattern(selectedType);
                float sweep = Mathf.Sin(elapsedFightTime * 2.4f + p * 0.65f) * cfg.sweepAmplitudeDeg;
                int count = Mathf.Max(1, cfg.projectilesPerPivot + bonusStreams);
                PathPatternType poolPattern = PathFormulaLibrary.MapToLegacyPattern(selectedType);

                for (int i = 0; i < count; i++)
                {
                    float spreadOffset = 0f;
                    if (count > 1)
                    {
                        float t = (float)i / (count - 1) - 0.5f;
                        spreadOffset = t * (cfg.sweepAmplitudeDeg * 2f);
                    }

                    // -90 deg points straight downward
                    float angleA = -90f + sweep + spreadOffset;

                    BulletPoolManager.Instance.SpawnBullet(
                        cfg.bulletVisual,
                        poolPattern,
                        config.pivotTransform.position,
                        angleA,
                        cfg.bulletSpeed,
                        cfg.bulletLifeTime,
                        15f,
                        playerTargetMask,
                        bossID
                    );
                }

                anyBulletFired = true;
            }

            currentSeed = ModuloLcgRandom.CurrentSeed;

            if (anyBulletFired)
            {
                AudioAndVFXManager.Instance?.PlayBossFireSFX();
            }
        }

        private BossPatternConfig GetConfigForPattern(BulletPathType pattern)
        {
            if (patternLookup.TryGetValue(pattern, out var cfg))
            {
                return cfg;
            }

            if (patternConfigs != null && patternConfigs.Length > 0)
            {
                return patternConfigs[0];
            }

            return new BossPatternConfig
            {
                patternName = pattern.ToString(),
                pattern = pattern,
                bulletVisual = BulletVisualType.Bullet_1,
                bulletSpeed = 2.4f,
                bulletLifeTime = 6.5f,
                sweepAmplitudeDeg = 16f,
                projectilesPerPivot = 5
            };
        }

        private void OnDisable()
        {
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
                damageFlashCoroutine = null;
            }
            RestoreOriginalSpriteColors();

            if (ModuloLcgRandom.CurrentSeed > 0)
            {
                PlayerPrefs.SetInt(CacheLastSeedKey, ModuloLcgRandom.CurrentSeed);
                ModuloLcgRandom.SaveSeed();
            }
        }

        public void TakeDamage(float amount)
        {
            if (currentHP <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            TriggerDamageFlash();
            HealthObserver.NotifyHealthChanged(bossID, currentHP, maxHP);

            if (currentHP <= 0f)
            {
                AudioAndVFXManager.Instance?.SpawnExplosionVFX(transform.position);
                GameManager.Instance?.OnEntityDied(bossID);
            }
        }

        private void TriggerDamageFlash()
        {
            if (cachedSpriteRenderers == null || cachedSpriteRenderers.Length == 0) return;
            if (!gameObject.activeInHierarchy) return;

            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
                RestoreOriginalSpriteColors();
            }

            damageFlashCoroutine = StartCoroutine(FlashDamageRedCoroutine());
        }

        private IEnumerator FlashDamageRedCoroutine()
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                if (cachedSpriteRenderers[i] != null)
                {
                    cachedSpriteRenderers[i].color = damageFlashColor;
                }
            }

            yield return new WaitForSeconds(damageFlashDuration);

            RestoreOriginalSpriteColors();
            damageFlashCoroutine = null;
        }

        private void RestoreOriginalSpriteColors()
        {
            if (cachedSpriteRenderers == null || originalSpriteColors == null) return;
            int count = Mathf.Min(cachedSpriteRenderers.Length, originalSpriteColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (cachedSpriteRenderers[i] != null)
                {
                    cachedSpriteRenderers[i].color = originalSpriteColors[i];
                }
            }
        }
    }
}
