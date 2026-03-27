using UnityEngine;

namespace Blackjack
{
    public class ChipButton : MonoBehaviour
    {
        [SerializeField] private ChipData chipData;

        public void OnClick()
        {
            if (chipData == null) return;
            GameManager.Instance.PlaceBet(chipData);
        }
    }
}