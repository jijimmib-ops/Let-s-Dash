using System;
using System.Collections.Generic;
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

        [Header("Extensible Upgrade Art Variants (Optional)")]
        [SerializeField] private CrateUpgradeArtConfig[] upgradeArtConfigs;

        [Header("Spawn Area Colliders")]
        [SerializeField] private Collider2D leftSpawnArea;
        [SerializeField] private Collider2D rightSpawnArea;

        private readonly List<WeaponUpgradeFlags> availableUpgrades = new List<WeaponUpgradeFlags>();
        private float spawnTimer;
        private bool spawnOnLeftNext = true;

        private void Awake()
        {
            BuildAvailableUpgradesList();
        }

        private void BuildAvailableUpgradesList()
        {
            availableUpgrades.Clear();

            // Prefer Inspector-configured upgradeArtConfigs when populated
            if (upgradeArtConfigs != null)
            {
                for (int i = 0; i < upgradeArtConfigs.Length; i++)
                {
                    WeaponUpgradeFlags flag = upgradeArtConfigs[i].upgradeFlag;
                    if (flag != WeaponUpgradeFlags.None && !availableUpgrades.Contains(flag))
                    {
                        availableUpgrades.Add(flag);
                    }
                }
            }

            // Fallback: dynamically discover all valid WeaponUpgradeFlags via Enum.GetValues
            if (availableUpgrades.Count == 0)
            {
                foreach (WeaponUpgradeFlags flag in Enum.GetValues(typeof(WeaponUpgradeFlags)))
                {
                    if (flag != WeaponUpgradeFlags.None)
                    {
                        availableUpgrades.Add(flag);
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
            if (availableUpgrades.Count == 0) BuildAvailableUpgradesList();
            if (availableUpgrades.Count == 0) return;

            Collider2D targetArea = spawnOnLeftNext ? leftSpawnArea : rightSpawnArea;
            spawnOnLeftNext = !spawnOnLeftNext;

            if (targetArea == null)
            {
                return;
            }

            Vector2 min = targetArea.bounds.min;
            Vector2 max = targetArea.bounds.max;

            Vector2 pos = new Vector2(
                ModuloLcgRandom.NextRange(min.x, max.x),
                ModuloLcgRandom.NextRange(min.y, max.y)
            );
            GameObject crateObj = Instantiate(cratePrefab, pos, Quaternion.identity, transform);

            int upgradeIdx = ModuloLcgRandom.NextMod(availableUpgrades.Count);
            WeaponUpgradeFlags selectedFlag = availableUpgrades[upgradeIdx];
            Sprite artSprite = FindSpriteForUpgrade(selectedFlag);

            var crate = crateObj.GetComponent<PowerUpCrate>();
            if (crate == null)
            {
                crate = crateObj.AddComponent<PowerUpCrate>();
            }
            crate.Initialize(selectedFlag, artSprite);
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
