using System;
using UnityEngine;
using LetsDash.Core;
using LetsDash.Entities;

namespace LetsDash.Items
{
    [Serializable]
    public struct CrateUpgradeArtConfig
    {
        public WeaponUpgradeFlags upgradeFlag;
        public Sprite sprite;
    }

    public class ParallelItemSpawner : MonoBehaviour
    {
        [Header("Crate Prefab & Spawn Interval")]
        [SerializeField] private GameObject cratePrefab;
        [SerializeField] private float spawnInterval = 15.0f;

        [Header("5 Upgrade Art Variants (Optional)")]
        [SerializeField] private CrateUpgradeArtConfig[] upgradeArtConfigs;

        [Header("Spawn Area Colliders")]
        [SerializeField] private Collider2D leftSpawnArea;
        [SerializeField] private Collider2D rightSpawnArea;

        [Header("Fallback Bounds (Used Only If Colliders Are Empty)")]
        [SerializeField] private Vector2 leftMinBounds = new Vector2(-7.5f, -4f);
        [SerializeField] private Vector2 leftMaxBounds = new Vector2(-1f, 0.5f);
        [SerializeField] private Vector2 rightMinBounds = new Vector2(1f, -4f);
        [SerializeField] private Vector2 rightMaxBounds = new Vector2(7.5f, 0.5f);

        private float spawnTimer;
        private bool spawnOnLeftNext = true;

        private static readonly WeaponUpgradeFlags[] AvailableUpgrades =
        {
            WeaponUpgradeFlags.Spread2Way,
            WeaponUpgradeFlags.Spread3Way,
            WeaponUpgradeFlags.ParallelOffset,
            WeaponUpgradeFlags.RapidFire,
            WeaponUpgradeFlags.DamageUp
        };

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                SpawnCrate();
            }
        }

        private void SpawnCrate()
        {
            if (cratePrefab == null) return;

            Collider2D targetArea = spawnOnLeftNext ? leftSpawnArea : rightSpawnArea;
            Vector2 min;
            Vector2 max;

            if (targetArea != null)
            {
                min = targetArea.bounds.min;
                max = targetArea.bounds.max;
            }
            else
            {
                min = spawnOnLeftNext ? leftMinBounds : rightMinBounds;
                max = spawnOnLeftNext ? leftMaxBounds : rightMaxBounds;
            }

            spawnOnLeftNext = !spawnOnLeftNext;

            Vector2 pos = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            GameObject crateObj = Instantiate(cratePrefab, pos, Quaternion.identity, transform);

            WeaponUpgradeFlags randomFlag = AvailableUpgrades[Random.Range(0, AvailableUpgrades.Length)];
            Sprite artSprite = FindSpriteForUpgrade(randomFlag);

            var crate = crateObj.GetComponent<PowerUpCrate>();
            if (crate == null)
            {
                crate = crateObj.AddComponent<PowerUpCrate>();
            }
            crate.Initialize(randomFlag, artSprite);
        }

        private Sprite FindSpriteForUpgrade(WeaponUpgradeFlags flag)
        {
            if (upgradeArtConfigs == null) return null;
            for (int i = 0; i < upgradeArtConfigs.Length; i++)
            {
                if (upgradeArtConfigs[i].upgradeFlag == flag)
                {
                    return upgradeArtConfigs[i].sprite;
                }
            }
            return null;
        }
    }

    [RequireComponent(typeof(Collider2D))]
    public class PowerUpCrate : MonoBehaviour
    {
        private WeaponUpgradeFlags upgradeFlag;

        public void Initialize(WeaponUpgradeFlags flag, Sprite overrideSprite = null)
        {
            upgradeFlag = flag;

            if (overrideSprite != null)
            {
                SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = overrideSprite;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                // Theme "Parallel": Picking up a crate upgrades the OTHER player
                player.CollectCrateAndUpgradePartner(upgradeFlag);
                Destroy(gameObject);
            }
        }
    }
}
