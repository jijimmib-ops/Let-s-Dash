using UnityEngine;
using LetsDash.Core;
using LetsDash.Entities;

namespace LetsDash.Items
{
    public class ParallelItemSpawner : MonoBehaviour
    {
        [Header("Crate Prefab & Spawn Interval")]
        [SerializeField] private GameObject cratePrefab;
        [SerializeField] private float spawnInterval = 15.0f;

        [Header("Arena Bounds")]
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

            Vector2 min = spawnOnLeftNext ? leftMinBounds : rightMinBounds;
            Vector2 max = spawnOnLeftNext ? leftMaxBounds : rightMaxBounds;
            spawnOnLeftNext = !spawnOnLeftNext;

            Vector2 pos = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            GameObject crateObj = Instantiate(cratePrefab, pos, Quaternion.identity, transform);

            WeaponUpgradeFlags randomFlag = AvailableUpgrades[Random.Range(0, AvailableUpgrades.Length)];
            var crate = crateObj.GetComponent<PowerUpCrate>();
            if (crate == null)
            {
                crate = crateObj.AddComponent<PowerUpCrate>();
            }
            crate.Initialize(randomFlag);
        }
    }

    [RequireComponent(typeof(Collider2D))]
    public class PowerUpCrate : MonoBehaviour
    {
        private WeaponUpgradeFlags upgradeFlag;

        public void Initialize(WeaponUpgradeFlags flag)
        {
            upgradeFlag = flag;
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
