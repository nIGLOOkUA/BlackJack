using UnityEngine;

namespace Blackjack
{
    public enum ChipColor
    {
        Red,
        Blue,
        Green,
        Black,
        Purple,
        Yellow,
        Brown
    }
    
    public class ChipData : ScriptableObject
    {
        public ChipColor color;
        public int value;
        public Sprite chipSprite;
    }
}