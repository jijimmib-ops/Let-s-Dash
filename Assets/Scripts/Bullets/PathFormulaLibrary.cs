using System;
using System.Collections.Generic;
using UnityEngine;
using LetsDash.Core;

namespace LetsDash.Bullets
{
    public class PathFormulaLibrary : MonoBehaviour
    {
        public static PathFormulaLibrary Instance { get; private set; }

        [Header("Deceleration Curve")]
        [SerializeField] private float referenceLifetime = 6.5f;

        // O(log n) Binary Search Tree lookup for all 5 BulletPathType trajectories (0..4)
        // Strict rule: Use ONLY Mathf.Cos and Mathf.Sin (NO Mathf.Tan to avoid asymptote infinities).
        private SortedDictionary<BulletPathType, Func<float, float, Vector2>> formulaTree;
        private List<BulletPathType> sortedKeys;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitializeFormulas();
        }

        public IReadOnlyList<BulletPathType> GetSortedPatternKeys()
        {
            if (formulaTree == null)
            {
                InitializeFormulas();
            }

            if (sortedKeys == null || sortedKeys.Count != formulaTree.Count)
            {
                sortedKeys = new List<BulletPathType>(formulaTree.Keys);
            }

            return sortedKeys;
        }

        public bool IsEmptySpace(BulletPathType type)
        {
            return type == BulletPathType.Space;
        }

        public bool TryGetFormula(BulletPathType type, out Func<float, float, Vector2> formula)
        {
            if (formulaTree == null)
            {
                InitializeFormulas();
            }

            return formulaTree.TryGetValue(type, out formula);
        }

        /// <summary>
        /// Applies a fast-to-slow ease-out deceleration curve over normalized bullet lifetime
        /// so projectiles burst out rapidly and slow down for fair dodging.
        /// </summary>
        public float ApplyDecelerationCurve(float t, float lifetime)
        {
            float safeLifetime = Mathf.Max(0.1f, lifetime);
            float normalizedT = Mathf.Clamp01(t / safeLifetime);
            float eased = 1f - Mathf.Pow(1f - normalizedT, 3f);
            float extraDrift = Mathf.Max(0f, t - safeLifetime) * 0.12f;
            return safeLifetime * 0.68f * eased + extraDrift;
        }

        public void InitializeFormulas()
        {
            formulaTree = new SortedDictionary<BulletPathType, Func<float, float, Vector2>>
            {
                // 0. LinearRay: Fast-to-slow straight trajectory along angle a (degrees)
                [BulletPathType.LinearRay] = (t, angleA) =>
                {
                    float d = ApplyDecelerationCurve(t, referenceLifetime);
                    float rad = angleA * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(rad) * d, Mathf.Sin(rad) * d);
                },

                // 1. HeartCardioidBurst: Fast-to-slow cardioid burst r = d * (1 - 0.5 * sin(a))
                [BulletPathType.HeartCardioidBurst] = (t, angleA) =>
                {
                    float d = ApplyDecelerationCurve(t, referenceLifetime);
                    float rad = angleA * Mathf.Deg2Rad;
                    float r = d * (1f - 0.5f * Mathf.Sin(rad));
                    return new Vector2(r * Mathf.Cos(rad), r * Mathf.Sin(rad));
                },

                // 2. CurvedArc: Fast-to-slow smooth trigonometric drift curving away from initial ray
                [BulletPathType.CurvedArc] = (t, angleA) =>
                {
                    float d = ApplyDecelerationCurve(t, referenceLifetime);
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float drift = 0.45f * (1f - Mathf.Cos(2.2f * d));
                    float u = d;
                    float v = drift * (rad >= 0f ? 1f : -1f);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 3. ArchimedeanSpiral: Fast-to-slow spiral r = d, theta = a + 3.2 * d
                [BulletPathType.ArchimedeanSpiral] = (t, angleA) =>
                {
                    float d = ApplyDecelerationCurve(t, referenceLifetime);
                    float rad = angleA * Mathf.Deg2Rad;
                    float theta = rad + 3.2f * d;
                    return new Vector2(d * Mathf.Cos(theta), d * Mathf.Sin(theta));
                },

                // 4. Space: Intentional rest/breather beat (no-op trajectory)
                [BulletPathType.Space] = (t, angleA) => Vector2.zero
            };
        }

        public Vector2 EvaluatePosition(BulletPathType type, float t, float angleA)
        {
            if (IsEmptySpace(type)) return Vector2.zero;

            if (TryGetFormula(type, out var formula) && formula != null)
            {
                return formula(t, angleA);
            }

            return Vector2.zero;
        }

        // Adapter overload so BulletPoolManager works seamlessly with PathPatternType
        public Vector2 EvaluatePosition(PathPatternType pattern, float t, float angleA)
        {
            return EvaluatePosition(MapFromLegacyPattern(pattern), t, angleA);
        }

        public static BulletPathType MapFromLegacyPattern(PathPatternType pattern)
        {
            return pattern switch
            {
                PathPatternType.LinearRay => BulletPathType.LinearRay,
                PathPatternType.HeartBurst => BulletPathType.HeartCardioidBurst,
                PathPatternType.CurvedArc => BulletPathType.CurvedArc,
                PathPatternType.Spiral => BulletPathType.ArchimedeanSpiral,
                _ => (BulletPathType)((int)pattern % 4)
            };
        }

        public static PathPatternType MapToLegacyPattern(BulletPathType pathType)
        {
            return pathType switch
            {
                BulletPathType.LinearRay => PathPatternType.LinearRay,
                BulletPathType.HeartCardioidBurst => PathPatternType.HeartBurst,
                BulletPathType.CurvedArc => PathPatternType.CurvedArc,
                BulletPathType.ArchimedeanSpiral => PathPatternType.Spiral,
                _ => PathPatternType.LinearRay
            };
        }
    }
}
