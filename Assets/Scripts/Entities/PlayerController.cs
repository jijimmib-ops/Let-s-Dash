using System;
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
        [SerializeField] private int playerID = 0; // 0 = Left (WASD+C), 1 = Right (Arrows+RightShift)
        [SerializeField] private PlayerController partnerPlayer;

        [Header("Stats")]
        [SerializeField] private float maxHP = 250f;
        [SerializeField] private float currentHP;
        [SerializeField] private float moveSpeed = 6.5f;
        [SerializeField] private float dashSpeedMultiplier = 2.6f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 1.1f;

        [Header("Arena Bounds")]
        [SerializeField] private Vector2 minBounds = new Vector2(-8f, -4.5f);
        [SerializeField] private Vector2 maxBounds = new Vector2(-0.5f, 1.5f);

        [Header("Single FirePivot & Weapon Modifiers")]
        [SerializeField] private Transform firePivot;
        [SerializeField] private float baseFireInterval = 0.24f;
        [SerializeField] private float baseDamage = 18f;
        [SerializeField] private float bulletSpeed = 8.5f;
        [SerializeField] private float parallelOffsetDistance = 0.28f;
        [SerializeField] private LayerMask bossLayerMask;
        [SerializeField] private WeaponUpgradeFlags activeUpgrades = WeaponUpgradeFlags.None;

        private float dashTimer;
        private float dashCooldownTimer;
        private float fireTimer;
        private Vector2 dashDirection;
        private bool isInvulnerable;

        public int EntityID => playerID;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public WeaponUpgradeFlags ActiveUpgrades => activeUpgrades;
        public bool IsInvulnerable => isInvulnerable;

        private void Awake()
        {
            currentHP = maxHP;
            if (firePivot == null)
            {
                firePivot = transform;
            }
        }

        private void Start()
        {
            HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            HandleMovementAndDash();
            AutoFireRoutine();
        }

        private Vector2 ReadInputAxis()
        {
            float x = 0f;
            float y = 0f;

            if (playerID == 0)
            {
                if (Input.GetKey(KeyCode.A)) x -= 1f;
                if (Input.GetKey(KeyCode.D)) x += 1f;
                if (Input.GetKey(KeyCode.W)) y += 1f;
                if (Input.GetKey(KeyCode.S)) y -= 1f;
            }
            else
            {
                if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            }

            return new Vector2(x, y).normalized;
        }

        private bool ReadDashPressed()
        {
            return playerID == 0 ? Input.GetKeyDown(KeyCode.C) : Input.GetKeyDown(KeyCode.RightShift);
        }

        private void HandleMovementAndDash()
        {
            float dt = Time.deltaTime;
            if (dashCooldownTimer > 0f) dashCooldownTimer -= dt;

            Vector2 input = ReadInputAxis();

            if (ReadDashPressed() && dashCooldownTimer <= 0f && dashTimer <= 0f)
            {
                dashDirection = input.sqrMagnitude > 0.01f ? input : Vector2.up;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
                isInvulnerable = true;
                AudioAndVFXManager.Instance?.SpawnDashVFX(transform.position);
                AudioAndVFXManager.Instance?.PlayDashSFX();
            }

            Vector2 velocity;
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                velocity = dashDirection * (moveSpeed * dashSpeedMultiplier);
                if (dashTimer <= 0f)
                {
                    isInvulnerable = false;
                }
            }
            else
            {
                velocity = input * moveSpeed;
            }

            Vector2 nextPos = (Vector2)transform.position + velocity * dt;
            nextPos.x = Mathf.Clamp(nextPos.x, minBounds.x, maxBounds.x);
            nextPos.y = Mathf.Clamp(nextPos.y, minBounds.y, maxBounds.y);
            transform.position = nextPos;
        }

        private void AutoFireRoutine()
        {
            if (BulletPoolManager.Instance == null || firePivot == null) return;

            // Pure functional rate and damage modifiers
            Func<float, float> rateModifier = interval =>
                activeUpgrades.HasFlag(WeaponUpgradeFlags.RapidFire) ? interval * 0.65f : interval;

            Func<float, float> damageModifier = dmg =>
                activeUpgrades.HasFlag(WeaponUpgradeFlags.DamageUp) ? dmg * 1.45f : dmg;

            fireTimer += Time.deltaTime;
            if (fireTimer < rateModifier(baseFireInterval)) return;
            fireTimer = 0f;

            // Pure function calculating X-axis parallel offsets from the SINGLE FirePivot
            Func<int, float> getParallelOffsetX = index =>
            {
                if (!activeUpgrades.HasFlag(WeaponUpgradeFlags.ParallelOffset)) return 0f;
                return index == 0 ? -parallelOffsetDistance : parallelOffsetDistance;
            };

            // Pure function calculating spread angles centered on 90 deg upward
            float[] spreadAngles = BuildSpreadAngles();
            int parallelCount = activeUpgrades.HasFlag(WeaponUpgradeFlags.ParallelOffset) ? 2 : 1;
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
            Func<float, float> clampSpread = angle => 90f + angle;

            if (activeUpgrades.HasFlag(WeaponUpgradeFlags.Spread3Way))
            {
                return new[] { clampSpread(-22f), clampSpread(0f), clampSpread(22f) };
            }
            if (activeUpgrades.HasFlag(WeaponUpgradeFlags.Spread2Way))
            {
                return new[] { clampSpread(-12f), clampSpread(12f) };
            }
            return new[] { clampSpread(0f) };
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
            AudioAndVFXManager.Instance?.SpawnPickupVFX(transform.position);
            AudioAndVFXManager.Instance?.PlayPickupSFX();
        }

        public void TakeDamage(float amount)
        {
            if (isInvulnerable || currentHP <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            HealthObserver.NotifyHealthChanged(playerID, currentHP, maxHP);
            AudioAndVFXManager.Instance?.PlayHitSFX();

            if (currentHP <= 0f)
            {
                AudioAndVFXManager.Instance?.SpawnExplosionVFX(transform.position);
                GameManager.Instance?.OnEntityDied(playerID);
            }
        }
    }
}
