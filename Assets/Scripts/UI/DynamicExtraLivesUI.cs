using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LetsDash.Entities;

namespace LetsDash.UI
{
    /// <summary>
    /// Observer subscriber displaying remaining Extra Lives (0..3) for a specific playerID.
    /// Supports 3 icon slots (e.g. Heart/Shield Images) and/or a numeric TMP_Text label.
    /// </summary>
    public class DynamicExtraLivesUI : MonoBehaviour
    {
        [Header("Target Player ID (0 = Left Player, 1 = Right Player)")]
        [SerializeField] private int targetPlayerID = 0;

        [Header("UI Elements (Assign Icons and/or Text)")]
        [SerializeField] private GameObject[] lifeIcons;
        [SerializeField] private TMP_Text livesText;

        private void OnEnable()
        {
            PlayerController.OnExtraLivesChanged += HandleExtraLivesChanged;
        }

        private void OnDisable()
        {
            PlayerController.OnExtraLivesChanged -= HandleExtraLivesChanged;
        }

        private void HandleExtraLivesChanged(int playerID, int remainingLives)
        {
            if (playerID != targetPlayerID) return;

            if (lifeIcons != null)
            {
                for (int i = 0; i < lifeIcons.Length; i++)
                {
                    if (lifeIcons[i] != null)
                    {
                        lifeIcons[i].SetActive(i < remainingLives);
                    }
                }
            }

            if (livesText != null)
            {
                livesText.text = $"{remainingLives}";
            }
        }
    }
}
