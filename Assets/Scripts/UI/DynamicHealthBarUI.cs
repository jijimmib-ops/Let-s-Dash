using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LetsDash.UI
{
    public class DynamicHealthBarUI : MonoBehaviour
    {
        [Header("Target Entity ID (0 = Left, 1 = Right, 2 = Boss)")]
        [SerializeField] private int targetEntityID = 0;

        [Header("UI Components")]
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text hpValueText;

        private void OnEnable()
        {
            HealthObserver.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            HealthObserver.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(int entityID, float currentHP, float maxHP)
        {
            if (entityID != targetEntityID) return;

            float ratio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;
            }

            if (hpValueText != null)
            {
                hpValueText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
            }
        }
    }
}
