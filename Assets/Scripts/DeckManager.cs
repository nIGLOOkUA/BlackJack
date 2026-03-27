using System.Collections.Generic;
using UnityEngine;

namespace Blackjack
{
    public class DeckManager : MonoBehaviour
    {
        private List<CardData> deck = new List<CardData>();
        private List<CardData> discardPile = new List<CardData>();

        [Header("Settings")]
        [SerializeField] private bool autoShuffleOnEmpty = true;

        public void InitializeDeck()
        {
            CardData[] loadedCards = Resources.LoadAll<CardData>("CardsData");

            if (loadedCards.Length == 0) return;

            deck = new List<CardData>(loadedCards);
            Shuffle();
        }

        public void Shuffle()
        {
            System.Random rng = new System.Random();
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CardData value = deck[k];
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        public CardData DrawCard()
        {
            if (deck.Count == 0)
            {
                if (autoShuffleOnEmpty && discardPile.Count > 0)
                {
                    ReshuffleDiscardPile();
                }
                else return null;
            }

            CardData card = deck[0];
            deck.RemoveAt(0);
            return card;
        }

        public void AddToDiscard(List<CardData> cards)
        {
            discardPile.AddRange(cards);
        }

        private void ReshuffleDiscardPile()
        {
            deck.AddRange(discardPile);
            discardPile.Clear();
            Shuffle();
        }
    }
}
