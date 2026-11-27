using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LetsDash.Core;
using LetsDash.Combat;
using LetsDash.Audio;

namespace LetsDash.Bullets
{
    [Serializable]
    public struct BulletPrefabConfig
    {
        public BulletVisualType visualType;
        public GameObject prefab; // Must have ONLY SpriteRenderer + BoxCollider2D
        public int initialPoolSize;
        public float hitRadius;
        public float scale;
    }

    public class BulletPoolManager : MonoBehaviour
    {
        public static BulletPoolManager Instance { get; private set; }

        [Header("Pool Configuration")]
        [SerializeField] private BulletPrefabConfig[] bulletConfigs;
        [SerializeField] private LayerMask wallBlockingMask;
        [SerializeField] private Transform poolContainer;

        private readonly List<BulletData> bulletPool = new List<BulletData>(2700);
        private bool isPrewarmed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (poolContainer == null)
            {
                poolContainer = transform;
            }
        }

        public IEnumerator PrewarmPoolCoroutine(Action<float> onProgress = null)
        {
            if (isPrewarmed)
            {
                onProgress?.Invoke(1f);
                yield break;
            }

            int totalToSpawn = 0;
            if (bulletConfigs != null)
            {
                totalToSpawn = bulletConfigs.Sum(c => Mathf.Max(0, c.initialPoolSize));
            }

            int spawned = 0;
            if (bulletConfigs != null)
            {
                foreach (var config in bulletConfigs)
                {
                    if (config.prefab == null) continue;

                    for (int i = 0; i < config.initialPoolSize; i++)
                    {
                        CreatePoolEntry(config);
                        spawned++;

                        if (spawned % 40 == 0)
                        {
                            onProgress?.Invoke(totalToSpawn > 0 ? (float)spawned / totalToSpawn : 1f);
                            yield return null;
                        }
                    }
                }
            }

            isPrewarmed = true;
            onProgress?.Invoke(1f);
        }

        private BulletData CreatePoolEntry(BulletPrefabConfig config)
        {
            GameObject obj = Instantiate(config.prefab, poolContainer);
            obj.transform.localScale = Vector3.one * (config.scale > 0f ? config.scale : 1f);
            obj.SetActive(false);

            var data = new BulletData
            {
                isActive = false,
                visualObj = obj,
                visualType = config.visualType,
                hitRadius = config.hitRadius > 0f ? config.hitRadius : 0.12f
            };
            bulletPool.Add(data);
            return data;
        }

        public BulletData SpawnBullet(
            BulletVisualType visualType,
            PathPatternType pattern,
            Vector2 origin,
            float angleA,
            float speed,
            float maxLifeTime,
            float damage,
            LayerMask targetLayerMask,
            int ownerEntityID)
        {
            // LINQ FirstOrDefault lookup for inactive pooled bullet of matching visual type
            BulletData bullet = bulletPool.FirstOrDefault(b => !b.isActive && b.visualType == visualType);

            if (bullet == null)
            {
                var config = bulletConfigs != null
                    ? bulletConfigs.FirstOrDefault(c => c.visualType == visualType)
                    : default;

                if (config.prefab != null)
                {
                    bullet = CreatePoolEntry(config);
                }
                else
                {
                    return null;
                }
            }

            bullet.isActive = true;
            bullet.originPos = origin;
            bullet.prevPos = origin;
            bullet.age_t = 0f;
            bullet.maxLifeTime = Mathf.Max(0.05f, maxLifeTime);
            bullet.angle_a = angleA;
            bullet.speed = speed;
            bullet.damage = damage;
            bullet.pattern = pattern;
            bullet.targetLayerMask = targetLayerMask;
            bullet.ownerEntityID = ownerEntityID;

            bullet.visualObj.transform.position = origin;
            bullet.visualObj.transform.rotation = Quaternion.Euler(0f, 0f, angleA);
            bullet.visualObj.SetActive(true);

            return bullet;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // LINQ .Where() iterates active bullets only
            foreach (BulletData bullet in bulletPool.Where(b => b.isActive))
            {
                if (!bullet.isActive) continue;

                bullet.age_t += dt;

                if (bullet.age_t >= bullet.maxLifeTime)
                {
                    ReturnToPool(bullet);
                    continue;
                }

                Vector2 localOffset = PathFormulaLibrary.Instance != null
                    ? PathFormulaLibrary.Instance.EvaluatePosition(bullet.pattern, bullet.age_t, bullet.angle_a) * bullet.speed
                    : new Vector2(Mathf.Cos(bullet.angle_a * Mathf.Deg2Rad), Mathf.Sin(bullet.angle_a * Mathf.Deg2Rad)) * (bullet.speed * bullet.age_t);

                Vector2 nextPos = bullet.originPos + localOffset;
                Vector2 delta = nextPos - bullet.prevPos;
                float distance = delta.magnitude;

                // Player bullets (ID 0 or 1) can intercept active Boss bullets (ID 2)
                if (bullet.ownerEntityID == 0 || bullet.ownerEntityID == 1)
                {
                    BulletData interceptedEnemy = bulletPool.FirstOrDefault(enemy =>
                        enemy.isActive &&
                        enemy.ownerEntityID == 2 &&
                        DistancePointToSegmentSq(enemy.prevPos, bullet.prevPos, nextPos) <=
                            (bullet.hitRadius + enemy.hitRadius) * (bullet.hitRadius + enemy.hitRadius));

                    if (interceptedEnemy != null)
                    {
                        AudioAndVFXManager.Instance?.SpawnImpactVFX(interceptedEnemy.prevPos);
                        ReturnToPool(interceptedEnemy);
                        ReturnToPool(bullet);
                        continue;
                    }
                }

                if (distance > 0.0001f)
                {
                    Vector2 dir = delta / distance;
                    int combinedMask = bullet.targetLayerMask | wallBlockingMask;
                    RaycastHit2D hit = Physics2D.CircleCast(bullet.prevPos, bullet.hitRadius, dir, distance, combinedMask);

                    if (hit.collider != null)
                    {
                        HitboxRelay relay = hit.collider.GetComponent<HitboxRelay>();
                        if (relay != null && relay.EntityID != bullet.ownerEntityID)
                        {
                            relay.ReceiveBulletHit(bullet.damage, hit.point);
                            AudioAndVFXManager.Instance?.SpawnImpactVFX(hit.point);
                            ReturnToPool(bullet);
                            continue;
                        }

                        // Hit Wall_CenterDivider or Arena Boundary
                        if (((1 << hit.collider.gameObject.layer) & wallBlockingMask) != 0)
                        {
                            ReturnToPool(bullet);
                            continue;
                        }
                    }
                }

                bullet.prevPos = nextPos;
                bullet.visualObj.transform.position = nextPos;
            }
        }

        private static float DistancePointToSegmentSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }

        public void ReturnToPool(BulletData bullet)
        {
            bullet.isActive = false;
            if (bullet.visualObj != null)
            {
                bullet.visualObj.SetActive(false);
            }
        }

        public void DeactivateAllBullets()
        {
            foreach (var bullet in bulletPool.Where(b => b.isActive))
            {
                ReturnToPool(bullet);
            }
        }
    }
}
