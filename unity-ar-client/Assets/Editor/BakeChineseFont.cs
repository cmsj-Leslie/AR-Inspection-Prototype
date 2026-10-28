#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Inspection.Editor
{
    public static class BakeChineseFont
    {
        [MenuItem("Tools/巡检系统/一键预烘焙常用汉字库 (Bake 3800 Characters)", false, 4)]
        public static void BakeCharacters()
        {
            string txtPath = "Assets/Fonts/ChineseChars.txt";
            string assetPath = "Assets/Fonts/simhei SDF.asset";

            if (!File.Exists(txtPath))
            {
                Debug.LogError("未找到 " + txtPath);
                return;
            }

            string characters = File.ReadAllText(txtPath, System.Text.Encoding.UTF8);

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
            {
                Debug.LogError("未找到 " + assetPath);
                return;
            }

            fontAsset.isMultiAtlasTexturesEnabled = true;
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            EditorUtility.DisplayProgressBar("预烘焙中文字库", "正在将 3800 个常用汉字与符号预加载到字体贴图中...", 0.3f);
            try
            {
                bool success = fontAsset.TryAddCharacters(characters, out string missing);
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=green>[中文字库烘焙] 成功预烘焙常用汉字！缺失字符数: {missing.Length}，字体资产现包含字符数: {fontAsset.characterTable.Count}</color>");
                EditorUtility.DisplayDialog("预烘焙成功", $"已成功将 {fontAsset.characterTable.Count} 个汉字与符号（覆盖所有界面及现场巡检输入）预写入字体贴图！\n现在按 Ctrl+Shift+B 打包运行，输入任何汉字都不会再出现方块！", "好");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
