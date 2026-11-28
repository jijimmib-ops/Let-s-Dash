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
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(HitboxRelay))]
    public class PlayerController : MonoBehaviour, IDamageableEntity
    {
        [Header("Identity & Partner")]
        [SerializeField] private int playerID = 0; // 0 = Left (WASD), 1 = Right (Arrows)
        [SerializeField] private PlayerController partnerPlayer;

        [Header("Stats & Extra Lives")]
        [SerializeField] private float maxHP = 250f;
        [SerializeField] private float currentHP;
        [SerializeField] private int extraLives = 3;
        [SerializeField] private float reviveInvulnerabilityDuration = 3f;
        [SerializeField] private float moveSpeed = 6.5f;

        [Header("Single FirePivot & Weapon Modifiers")]
        [SerializeField] private Transform firePivot;
        [SerializeField] private float baseFireInterval = 0.24f;
        [SerializeField] private float baseDamage = 18f;
        [SerializeField] private float bulletSpeed = 8.5f;
        [SerializeField] private float parallelOffsetDistance = 0.28f;
        [SerializeField] private LayerMask bossLayerMask;
        [SerializeField] private WeaponUpgradeFlags activeUpgrades = WeaponUpgradeFlags.None;

        private float reviveInvulnTimer;
        private float fireTimer;
        private bool isInvulnerable;
        private Rigidbody2D rb;

        private readonly Dictionary<WeaponUpgradeFlags, int> upgradeStacks = new Dictionary<WeaponUpgradeFlags, int>();

        public int EntityID => playerID;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public int ExtraLives => extraLives;
        public WeaponUpgradeFlags ActiveUpgrades => activeUpgrades;
        public bool IsInvulnerable => isInvulnerable;

        public static event Action<int, int> OnExtraLivesChanged;
        public static event Action<int, WeaponUpgradeFlags, int> OnUpgradeStacked;

        public int GetUpgradeStack(WeaponUpgradeFlags flag)
        {
            return upgradeStacks.TryGetValue(flag, out int count) ? count : 0;
        }

        private void Awake()
        {
            currentHP = maxHP;
            rb = GetComponent<Rigidbody2D>();
            if (firePivot == null)
            {
                firePivot = transform;
            }

            // Seed stack counts if any flags were pre-enabled in the Inspector
            foreach (WeaponUpgradeFlags flag in Enum.GetValues(typeof(WeaponUpgradeFlags)))
            {
                if (flag != WeaponUpgradeFlags.None && activeUpgrades.HasFlag(flag))
                {
                    upgradeStacks[flag] = 1;
                }
            }
        }

        private void Start()
        {
            HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
            OnExtraLivesChanged?.Invoke(playerID, extraLives);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            HandleMovement();
            AutoFireRoutine();
        }

        private Vector2 ReadInputAxis()
        {
            float x = 0f;
            float y = 0f;

            if (playerID == 0)
            {
                if (Input.GetKeyDown(KeyCode.W)) ModuloLcgRandom.FeedInput(1);
                if (Input.GetKeyDown(KeyCode.A)) ModuloLcgRandom.FeedInput(2);
                if (Input.GetKeyDown(KeyCode.S)) ModuloLcgRandom.FeedInput(3);
                if (Input.GetKeyDown(KeyCode.D)) ModuloLcgRandom.FeedInput(4);

                if (Input.GetKey(KeyCode.A)) x -= 1f;
                if (Input.GetKey(KeyCode.D)) x += 1f;
                if (Input.GetKey(KeyCode.W)) y += 1f;
                if (Input.GetKey(KeyCode.S)) y -= 1f;
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) ModuloLcgRandom.FeedInput(5);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) ModuloLcgRandom.FeedInput(6);
                if (Input.GetKeyDown(KeyCode.DownArrow)) ModuloLcgRandom.FeedInput(7);
                if (Input.GetKeyDown(KeyCode.RightArrow)) ModuloLcgRandom.FeedInput(8);

                if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            }

            return new Vector2(x, y).normalized;
        }

        private void HandleMovement()
        {
            float dt = Time.deltaTime;
            if (reviveInvulnTimer > 0f) reviveInvulnTimer -= dt;

            Vector2 input = ReadInputAxis();
            Vector2 velocity = input * moveSpeed;

            isInvulnerable = reviveInvulnTimer > 0f;

            if (rb != null)
            {
                // Let 4-sided Wall BoxCollider2Ds & Wall_CenterDivider constrain movement via Physics2D
                rb.linearVelocity = velocity;
            }
            else
            {
                transform.position = (Vector2)transform.position + velocity * dt;
            }
        }

        private void AutoFireRoutine()
        {
            if (BulletPoolManager.Instance == null || firePivot == null) return;

            int rapidStacks = GetUpgradeStack(WeaponUpgradeFlags.RapidFire);
            int damageStacks = GetUpgradeStack(WeaponUpgradeFlags.DamageUp);
            int parallelStacks = GetUpgradeStack(WeaponUpgradeFlags.ParallelOffset);

            // Pure functional rate and damage modifiers (stackable)
            Func<float, float> rateModifier = interval =>
                rapidStacks > 0 ? interval * Mathf.Max(0.28f, Mathf.Pow(0.82f, rapidStacks)) : interval;

            Func<float, float> damageModifier = dmg =>
                damageStacks > 0 ? dmg * (1f + 0.35f * damageStacks) : dmg;

            fireTimer += Time.deltaTime;
            if (fireTimer < rateModifier(baseFireInterval)) return;
            fireTimer = 0f;

            // Pure function calculating symmetric X-axis parallel offsets from the SINGLE FirePivot (1..5 rails)
            int parallelCount = Mathf.Min(5, 1 + parallelStacks);
            Func<int, float> getParallelOffsetX = index =>
                (index - (parallelCount - 1) * 0.5f) * parallelOffsetDistance;

            // Pure function calculating spread angles centered on 90 deg upward
            float[] spreadAngles = BuildSpreadAngles();
            float finalDamage = damageModifier(baseDamage);

            for (int p = 0; p < parallelCount; p++)
            {
                float offsetX = getParallelOffsetX(p);
                Vector2 spawnPos = (Vector2)firePivot.position + new Vector2(offsetX, 0f);

                for (int s = 0; s < spreadAngles.Length; s++)
                {
                    BulletPoolManager.Instance.SpawnBullet(
                        BulletVisualType.Bullet_1,
                        PathPatternType.LinearRay,
                        spawnPos,
                        spreadAngles[s],
                        bulletSpeed,
                        2.2f,
                        finalDamage,
                        bossLayerMask,
                        playerID
                    );
                }
            }

            AudioAndVFXManager.Instance?.PlayPlayerFireSFX();
        }

        private float[] BuildSpreadAngles()
        {
            int spread2Stacks = GetUpgradeStack(WeaponUpgradeFlags.Spread2Way);
            int spread3Stacks = GetUpgradeStack(WeaponUpgradeFlags.Spread3Way);
            int totalWays = Mathf.Min(9, 1 + spread2Stacks + spread3Stacks * 2);

            if (totalWays <= 1)
            {
                return new[] { 90f };
            }

            float halfSpan = Mathf.Min(38f, (totalWays - 1) * 9f);
            float[] angles = new float[totalWays];
            for (int i = 0; i < totalWays; i++)
            {
                float t = (float)i / (totalWays - 1);
                angles[i] = 90f + Mathf.Lerp(-halfSpan, halfSpan, t);
            }
            return angles;
        }

        public void CollectCrateAndUpgradePartner(WeaponUpgradeFlags flag)
        {
            if (partnerPlayer != null)
            {
                partnerPlayer.ReceivePartnerUpgrade(flag);
            }
            else
            {
                ReceivePartnerUpgrade(flag);
            }
        }

        public void ReceivePartnerUpgrade(WeaponUpgradeFlags flag)
        {
            activeUpgrades |= flag;
            int nextLevel = GetUpgradeStack(flag) + 1;
            upgradeStacks[flag] = nextLevel;
            OnUpgradeStacked?.Invoke(playerID, flag, nextLevel);
            AudioAndVFXManager.Instance?.SpawnPickupVFX(transform.position);
            AudioAndVFXManager.Instance?.PlayPickupSFX();
        }

        public void TakeDamage(float amount)
        {
            if (isInvulnerable || currentHP <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            AudioAndVFXManager.Instance?.PlayHitSFX();

            if (currentHP <= 0f)
            {
                if (extraLives > 0)
                {
                    // Extra Life Revive: restore full HP and grant 3s temporary invulnerability
                    extraLives--;
                    currentHP = maxHP;
                    reviveInvulnTimer = reviveInvulnerabilityDuration;
                    isInvulnerable = true;

                    OnExtraLivesChanged?.Invoke(playerID, extraLives);
                    HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
                    AudioAndVFXManager.Instance?.SpawnPickupVFX(transform.position);
                    return;
                }

                HealthObserver.NotifyHealthChanged(playerID, 0f, maxHP);
                AudioAndVFXManager.Instance?.SpawnExplosionVFX(transform.position);
                GameManager.Instance?.OnEntityDied(playerID);
                return;
            }

            HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
        }
    }
}
