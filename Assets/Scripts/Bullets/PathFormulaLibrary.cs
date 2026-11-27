using System;
using System.Collections.Generic;
using UnityEngine;
using LetsDash.Core;

namespace LetsDash.Bullets
{
    public class PathFormulaLibrary : MonoBehaviour
    {
        public static PathFormulaLibrary Instance { get; private set; }

        // O(log n) Binary Search Tree lookup for Desmos-style parametric equations (x(t, a), y(t, a))
        // Strict rule: Use ONLY Mathf.Cos and Mathf.Sin (NO Mathf.Tan to avoid asymptote infinities).
        private SortedDictionary<PathPatternType, Func<float, float, Vector2>> formulaTree;
        private List<PathPatternType> sortedKeys;

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

        public IReadOnlyList<PathPatternType> GetSortedPatternKeys()
        {
            if (formulaTree == null)
            {
                InitializeFormulas();
            }

            if (sortedKeys == null || sortedKeys.Count != formulaTree.Count)
            {
                sortedKeys = new List<PathPatternType>(formulaTree.Keys);
            }

            return sortedKeys;
        }

        public void InitializeFormulas()
        {
            formulaTree = new SortedDictionary<PathPatternType, Func<float, float, Vector2>>
            {
                // 1. Linear Ray: Straight trajectory along angle a (in degrees)
                [PathPatternType.LinearRay] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(rad) * t, Mathf.Sin(rad) * t);
                },

                // 2. Rotated Sine Wave: u = t, v = 0.55 * sin(7t), rotated by angle a
                [PathPatternType.SineWave] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t;
                    float v = 0.55f * Mathf.Sin(7f * t);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 3. Archimedean Spiral: r = t, theta = 3.2t + a
                [PathPatternType.Spiral] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float theta = rad + 3.2f * t;
                    float r = t;
                    return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
                },

                // 4. Curved Arc: Smooth trigonometric drift curving away from initial ray
                [PathPatternType.CurvedArc] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float drift = 0.45f * (1f - Mathf.Cos(2.2f * t));
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t;
                    float v = drift * Mathf.Sin(rad >= 0f ? 1.570796f : -1.570796f);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 5. Touhou Rose Curve: r = (0.65 + 0.35 * cos(5 * theta)) * t
                [PathPatternType.RoseCurve] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float theta = rad + 1.4f * t;
                    float r = (0.65f + 0.35f * Mathf.Cos(5f * theta)) * t;
                    return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
                },

                // 6. Logarithmic Spiral: r = t, theta = a + 2.5 * ln(1 + 2t)
                [PathPatternType.LogarithmicSpiral] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float theta = rad + 2.5f * Mathf.Log(1f + 2f * t);
                    return new Vector2(t * Mathf.Cos(theta), t * Mathf.Sin(theta));
                },

                // 7. Damped Wave: u = t, v = 0.8 * exp(-1.5t) * sin(10t), rotated by angle a
                [PathPatternType.DampedWave] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t;
                    float v = 0.8f * Mathf.Exp(-1.5f * t) * Mathf.Sin(10f * t);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 8. Expanding Helix / Coil: u = t + 0.25*cos(12t), v = 0.25*sin(12t)
                [PathPatternType.ExpandingHelix] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t + 0.25f * Mathf.Cos(12f * t);
                    float v = 0.25f * Mathf.Sin(12f * t);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 9. Zig-Zag / Sawtooth Wave: u = t, v = 0.4 * arcsin(sin(6t))
                [PathPatternType.ZigZagWave] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t;
                    float v = 0.4f * Mathf.Asin(Mathf.Clamp(Mathf.Sin(6f * t), -1f, 1f));
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                },

                // 10. Heart / Cardioid Burst: r = t * (1 - 0.5 * sin(a))
                [PathPatternType.HeartBurst] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float r = t * (1f - 0.5f * Mathf.Sin(rad));
                    return new Vector2(r * Mathf.Cos(rad), r * Mathf.Sin(rad));
                },

                // 11. Starburst Polygon (5-point): r = t / cos((2/5) * arcsin(sin((5/2) * (a + t))))
                [PathPatternType.StarburstPolygon] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float theta = rad + t;
                    float s = Mathf.Clamp(Mathf.Sin(2.5f * theta), -1f, 1f);
                    float denom = Mathf.Max(0.1f, Mathf.Cos(0.4f * Mathf.Asin(s)));
                    float r = t / denom;
                    return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
                },

                // 12. Boomerang / Hook: u = t - 0.6*t^2, v = 0.8*sin(2.5t)
                [PathPatternType.BoomerangHook] = (t, angleA) =>
                {
                    float rad = angleA * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(rad);
                    float sinA = Mathf.Sin(rad);
                    float u = t - 0.6f * t * t;
                    float v = 0.8f * Mathf.Sin(2.5f * t);
                    return new Vector2(u * cosA - v * sinA, u * sinA + v * cosA);
                }
            };
        }

        public Vector2 EvaluatePosition(PathPatternType type, float t, float angleA)
        {
            if (formulaTree == null)
            {
                InitializeFormulas();
            }

            if (formulaTree.TryGetValue(type, out var formula))
            {
                return formula(t, angleA);
            }

            return Vector2.zero;
        }
    }
}
