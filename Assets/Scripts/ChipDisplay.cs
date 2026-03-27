using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blackjack
{
    public class ChipDisplay : MonoBehaviour
    {
        [SerializeField] private Image chipImage;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI valueText;

        private ChipData data;

        public void Setup(ChipData _data, System.Action<ChipData> onChipClicked)
        {
            data = _data;

            if (chipImage != null)
            {
                chipImage.sprite = data.chipSprite;
            }

            if (valueText != null) 
            {
                valueText.text = data.value.ToString();
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                // Передаємо весь об'єкт даних
                button.onClick.AddListener(() => onChipClicked?.Invoke(data));
            }
        }
    }
}