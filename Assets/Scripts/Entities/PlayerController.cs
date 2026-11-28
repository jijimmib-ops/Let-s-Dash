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

        [Header("Damage Flash")]
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private float damageFlashDuration = 0.1f;

        private float reviveInvulnTimer;
        private float fireTimer;
        private bool isInvulnerable;
        private Rigidbody2D rb;
        private SpriteRenderer[] cachedSpriteRenderers;
        private Color[] originalSpriteColors;
        private Coroutine damageFlashCoroutine;

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
            // Enforce strict Player 0 (Left) / Player 1 (Right) identity from GameObject name
            if (gameObject.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                playerID = 1;
            }
            else if (gameObject.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                playerID = 0;
            }

            currentHP = maxHP;
            rb = GetComponent<Rigidbody2D>();
            if (firePivot == null)
            {
                firePivot = transform;
            }

            cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalSpriteColors = new Color[cachedSpriteRenderers.Length];
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                originalSpriteColors[i] = cachedSpriteRenderers[i].color;
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
                // Player 0 (Left): strictly W, A, S, D only
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
                // Player 1 (Right): strictly Arrow Keys only
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
                // Outer 4-sided Wall_Boundaries BoxCollider2Ds constrain movement across the shared arena
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
            int doubleWayStacks = GetUpgradeStack(WeaponUpgradeFlags.DoubleWay);

            // Pure functional rate and damage modifiers (stackable)
            Func<float, float> rateModifier = interval =>
                rapidStacks > 0 ? interval * Mathf.Max(0.28f, Mathf.Pow(0.82f, rapidStacks)) : interval;

            Func<float, float> damageModifier = dmg =>
                damageStacks > 0 ? dmg * (1f + 0.35f * damageStacks) : dmg;

            fireTimer += Time.deltaTime;
            if (fireTimer < rateModifier(baseFireInterval)) return;
            fireTimer = 0f;

            // DoubleWay stacks add +1 parallel firing lane per stack from the SINGLE FirePivot
            int parallelCount = 1 + doubleWayStacks;
            Func<int, float> getParallelOffsetX = index =>
                (index - (parallelCount - 1) * 0.5f) * parallelOffsetDistance;

            // SpreadPlusOne stacks add +1 spread direction per stack centered on 90 deg upward
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
            int spreadStacks = GetUpgradeStack(WeaponUpgradeFlags.SpreadPlusOne);
            int totalWays = 1 + spreadStacks;

            if (totalWays <= 1)
            {
                return new[] { 90f };
            }

            float halfSpan = Mathf.Min(42f, (totalWays - 1) * 8f);
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

        private void OnDisable()
        {
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
                damageFlashCoroutine = null;
            }
            RestoreOriginalSpriteColors();
        }

        public void TakeDamage(float amount)
        {
            if (isInvulnerable || currentHP <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            TriggerDamageFlash();
            AudioAndVFXManager.Instance?.PlayHitSFX();

            if (currentHP <= 0f)
            {
                if (extraLives > 0)
                {
                    // Extra Life Revive (VFX #4): restore full HP and grant 3s temporary invulnerability
                    extraLives--;
                    currentHP = maxHP;
                    reviveInvulnTimer = reviveInvulnerabilityDuration;
                    isInvulnerable = true;

                    OnExtraLivesChanged?.Invoke(playerID, extraLives);
                    HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
                    AudioAndVFXManager.Instance?.SpawnReviveVFX(transform.position);
                    return;
                }

                // Dual Player Death (VFX #5): play simultaneously at BOTH Player_Left and Player_Right
                HealthObserver.NotifyHealthChanged(playerID, 0f, maxHP);
                AudioAndVFXManager.Instance?.SpawnDualDeathVFX(transform.position);
                if (partnerPlayer != null)
                {
                    AudioAndVFXManager.Instance?.SpawnDualDeathVFX(partnerPlayer.transform.position);
                }
                GameManager.Instance?.OnEntityDied(playerID);
                return;
            }

            HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
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
