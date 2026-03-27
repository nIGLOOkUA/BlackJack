#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Blackjack
{
    public class CardImporter
    {
        [MenuItem("Tools/Generate Cards")]
        public static void GenerateCards()
        {
            string spriteFolder = "Assets/Cards";
            string dataFolder = "Assets/Resources/CardsData";

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
                AssetDatabase.Refresh();
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (!TryParseCard(sprite.name, out CardSuit suit, out CardRank rank))
                {
                    Debug.LogWarning($"Не вдалося розпізнати карту: {sprite.name}");
                    continue;
                }

                CardData card = ScriptableObject.CreateInstance<CardData>();
                card.suit = suit;
                card.rank = rank;
                card.cardSprite = sprite;

                string assetPath = $"{dataFolder}/{sprite.name}.asset";
                AssetDatabase.CreateAsset(card, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Карти згенеровано");
        }

        private static bool TryParseCard(string fileName, out CardSuit suit, out CardRank rank)
        {
            suit = CardSuit.Spades;
            rank = CardRank.Two;

            string[] parts = fileName.Split('_');
            if (parts.Length != 2)
                return false;

            string suitPart = parts[0].ToLower();
            string rankPart = parts[1].ToLower();

            switch (suitPart)
            {
                case "hearts": suit = CardSuit.Hearts; break;
                case "diamonds": suit = CardSuit.Diamonds; break;
                case "clubs": suit = CardSuit.Clubs; break;
                case "spades": suit = CardSuit.Spades; break;
                default: return false;
            }

            switch (rankPart)
            {
                case "2": rank = CardRank.Two; break;
                case "3": rank = CardRank.Three; break;
                case "4": rank = CardRank.Four; break;
                case "5": rank = CardRank.Five; break;
                case "6": rank = CardRank.Six; break;
                case "7": rank = CardRank.Seven; break;
                case "8": rank = CardRank.Eight; break;
                case "9": rank = CardRank.Nine; break;
                case "10": rank = CardRank.Ten; break;
                case "j": rank = CardRank.Jack; break;
                case "q": rank = CardRank.Queen; break;
                case "k": rank = CardRank.King; break;
                case "a": rank = CardRank.Ace; break;
                default: return false;
            }
            return true;
        }
    }
}
#endif