using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    public class CardDisplay : MonoBehaviour
    {
        [SerializeField] private Image cardImage;
        
        public CardData Data { get; private set; }
        private Sprite faceSprite;
        public bool IsFaceDown { get; private set; }

        private void Awake()
        {
            if (cardImage == null) cardImage = GetComponent<Image>();
        }

        public void Setup(CardData data, Sprite backSprite, bool startFaceDown)
        {
            Data = data;
            faceSprite = data.cardSprite;
            IsFaceDown = startFaceDown;

            if (cardImage != null)
            {
                if (startFaceDown) cardImage.sprite = backSprite;
                else cardImage.sprite = faceSprite;
                cardImage.preserveAspect = true;
            }
        }

        public void Flip()
        {
            if (IsFaceDown)
            {
                IsFaceDown = false;
                
                if (cardImage != null)
                {
                    cardImage.sprite = faceSprite;
                }
            }
        }
    }
}