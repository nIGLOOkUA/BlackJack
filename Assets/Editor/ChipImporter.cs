#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Blackjack
{
    public class ChipImporter
    {
        [MenuItem("Tools/Generate Chips")]
        public static void GenerateChips()
        {
            string spriteFolder = "Assets/Chips";
            string dataFolder = "Assets/Resources/ChipsData";

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

                if (!TryParseChip(sprite.name, out ChipColor color, out int value))
                {
                    Debug.LogWarning($"Не вдалося розпізнати фішку: {sprite.name}");
                    continue;
                }

                ChipData chip = ScriptableObject.CreateInstance<ChipData>();
                chip.color = color;
                chip.value = value;
                chip.chipSprite = sprite;

                string assetPath = $"{dataFolder}/{sprite.name}.asset";
                AssetDatabase.CreateAsset(chip, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Фішки згенеровано");
        }

        private static bool TryParseChip( string fileName, out ChipColor color, out int value)
        {
            color = ChipColor.Red;
            value = 0;

            string[] parts = fileName.Split('_');
            if (parts.Length != 2 || parts[1] != "chip") return false;

            string colorPart = parts[0].ToLower();

            switch (colorPart)
            {
                case "red":
                    color = ChipColor.Red;
                    value = 5;
                    break;
                case "blue":
                    color = ChipColor.Blue;
                    value = 10;
                    break;
                case "green":
                    color = ChipColor.Green;
                    value = 25;
                    break;
                case "black":
                    color = ChipColor.Black;
                    value = 100;
                    break;
                case "purple":
                    color = ChipColor.Purple;
                    value = 500;
                    break;
                case "yellow":
                    color = ChipColor.Yellow;
                    value = 1000;
                    break;
                case "brown":
                    color = ChipColor.Brown;
                    value = 5000;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
#endif