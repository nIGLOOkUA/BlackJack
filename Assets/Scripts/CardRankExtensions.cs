using System;

namespace Blackjack
{
    public static class CardExtensions
    {
        public static int GetBlackjackValue(this CardRank rank) => rank switch
        {
            CardRank.Two => 2,
            CardRank.Three => 3,
            CardRank.Four => 4,
            CardRank.Five => 5,
            CardRank.Six => 6,
            CardRank.Seven => 7,
            CardRank.Eight => 8,
            CardRank.Nine => 9,
            CardRank.Ten or CardRank.Jack or CardRank.Queen or CardRank.King => 10,
            CardRank.Ace => 11,
            _ => 0
        };
    }
}