using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LetsDash.Audio
{
    public class AudioAndVFXManager : MonoBehaviour
    {
        public static AudioAndVFXManager Instance { get; private set; }

        [Header("Audio Sources & Clips")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip playerFireClip;
        [SerializeField] private AudioClip bossFireClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip pickupClip;

        [Header("Pooled VFX Prefabs (5 Slots)")]
        [SerializeField] private GameObject impactVfxPrefab;     // 1. Impact VFX
        [SerializeField] private GameObject explosionVfxPrefab;  // 2. Player/Boss Explosion VFX
        [SerializeField] private GameObject pickupVfxPrefab;     // 3. Pickup VFX
        [SerializeField] private GameObject reviveVfxPrefab;     // 4. Extra Life Revive VFX
        [SerializeField] private GameObject dualDeathVfxPrefab;  // 5. Dual Player Death VFX
        [SerializeField] private int vfxPoolSizePerType = 16;

        private readonly Dictionary<GameObject, List<GameObject>> vfxPools = new Dictionary<GameObject, List<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void PrewarmVFXPools()
        {
            PrewarmPrefab(impactVfxPrefab);
            PrewarmPrefab(explosionVfxPrefab);
            PrewarmPrefab(pickupVfxPrefab);
            PrewarmPrefab(reviveVfxPrefab);
            PrewarmPrefab(dualDeathVfxPrefab);
        }

        private void PrewarmPrefab(GameObject prefab)
        {
            if (prefab == null || vfxPools.ContainsKey(prefab)) return;

            var list = new List<GameObject>(vfxPoolSizePerType);
            for (int i = 0; i < vfxPoolSizePerType; i++)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                list.Add(instance);
            }
            vfxPools[prefab] = list;
        }

        public void PlayBGM()
        {
            if (bgmSource != null && bgmClip != null)
            {
                bgmSource.clip = bgmClip;
                bgmSource.loop = true;
                if (!bgmSource.isPlaying)
                {
                    bgmSource.Play();
                }
            }
        }

        public void StopBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }
        }

        public void PlayPlayerFireSFX() => PlayOneShot(playerFireClip, 0.25f);
        public void PlayBossFireSFX() => PlayOneShot(bossFireClip, 0.35f);
        public void PlayHitSFX() => PlayOneShot(hitClip, 0.7f);
        public void PlayPickupSFX() => PlayOneShot(pickupClip, 0.8f);

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (sfxSource != null && clip != null)
            {
                sfxSource.PlayOneShot(clip, volume);
            }
        }

        public void SpawnImpactVFX(Vector2 pos) => SpawnVFX(impactVfxPrefab, pos, 0.35f);
        public void SpawnExplosionVFX(Vector2 pos) => SpawnVFX(explosionVfxPrefab, pos, 0.9f);
        public void SpawnPickupVFX(Vector2 pos) => SpawnVFX(pickupVfxPrefab, pos, 0.55f);
        public void SpawnReviveVFX(Vector2 pos) => SpawnVFX(reviveVfxPrefab, pos, 0.85f);
        public void SpawnDualDeathVFX(Vector2 pos) => SpawnVFX(dualDeathVfxPrefab, pos, 1.0f);

        private void SpawnVFX(GameObject prefab, Vector2 pos, float duration)
        {
            if (prefab == null) return;
            if (!vfxPools.TryGetValue(prefab, out var pool))
            {
                PrewarmPrefab(prefab);
                pool = vfxPools[prefab];
            }

            GameObject vfx = pool.FirstOrDefault(v => !v.activeSelf);
            if (vfx == null)
            {
                vfx = Instantiate(prefab, transform);
                pool.Add(vfx);
            }

            vfx.transform.position = pos;
            vfx.SetActive(true);
            StartCoroutine(ReturnVFXAfterDelay(vfx, duration));
        }

        private static IEnumerator ReturnVFXAfterDelay(GameObject vfx, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (vfx != null)
            {
                vfx.SetActive(false);
            }
        }
    }
}
