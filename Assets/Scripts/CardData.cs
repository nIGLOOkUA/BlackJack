using UnityEngine;

namespace Blackjack
{
    public enum CardSuit
    {
        Hearts, 
        Diamonds, 
        Clubs, 
        Spades
    }

    public enum CardRank
    {
        Two, 
        Three, 
        Four, 
        Five, 
        Six, 
        Seven, 
        Eight,
        Nine, 
        Ten,
        Jack, 
        Queen, 
        King, 
        Ace
    }
    
    public class CardData : ScriptableObject
    {
        public CardSuit suit;
        public CardRank rank;
        public Sprite cardSprite;
    }
}