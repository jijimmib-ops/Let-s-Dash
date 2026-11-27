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

        [Header("Stage Progression (Deterministic Modulo)")]
        [SerializeField] private BossStageConfig[] stages;
        [SerializeField] private float stageDuration = 12f;
        [SerializeField] private int currentSeed = 7;

        private int currentStageIndex;
        private float stageTimer;
        private float fireTimer;
        private float elapsedFightTime;

        public int EntityID => bossID;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;

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
                }
            };
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
            if (stages == null || stages.Length == 0) return;
            int len = stages.Length;
            int val = currentSeed + Mathf.FloorToInt(elapsedFightTime * 0.5f) + 1;
            currentSeed = val;
            // Guaranteed positive modulo index
            currentStageIndex = (val % len + len) % len;
        }

        public void ExecuteFirePattern(BossStageConfig stage)
        {
            if (BulletPoolManager.Instance == null || pivotPoints.Count == 0) return;

            float sweep = Mathf.Sin(elapsedFightTime * 2.4f) * stage.sweepAmplitudeDeg;

            for (int p = 0; p < pivotPoints.Count; p++)
            {
                Transform pivot = pivotPoints[p];
                if (pivot == null) continue;

                // Left pivots target Left player mask; Right pivots target Right player mask
                bool isLeftSide = pivot.position.x < 0f;
                LayerMask targetMask = isLeftSide ? leftPlayerMask : rightPlayerMask;

                int count = Mathf.Max(1, stage.projectilesPerPivot);
                for (int i = 0; i < count; i++)
                {
                    float spreadOffset = 0f;
                    if (count > 1)
                    {
                        float t = (float)i / (count - 1) - 0.5f;
                        spreadOffset = t * (stage.sweepAmplitudeDeg * 2f);
                    }

                    // -90 deg points straight downward
                    float angleA = -90f + sweep + spreadOffset;

                    BulletPoolManager.Instance.SpawnBullet(
                        stage.bulletVisual,
                        stage.pattern,
                        pivot.position,
                        angleA,
                        stage.bulletSpeed,
                        stage.bulletLifeTime,
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
