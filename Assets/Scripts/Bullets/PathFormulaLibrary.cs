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
