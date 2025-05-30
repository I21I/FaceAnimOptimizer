using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FaceAnimOptimizer
{
    public enum Language
    {
        [InspectorName("日本語")]
        Japanese,
        [InspectorName("English")]
        English
    }

    public static class FaceAnimOptimizerLocalization
    {
        private static Dictionary<string, Dictionary<Language, string>> localizedStrings = new Dictionary<string, Dictionary<Language, string>>();
        public static Language CurrentLanguage { get; set; } = Language.Japanese;

        public static void Initialize()
        {
            if (localizedStrings.Count > 0)
                return;

            AddLocalization("WindowTitle", "FaceAnim Optimizer", "FaceAnim Optimizer");
            
            AddLocalization("TargetMesh", "対象メッシュ", "Target Mesh");
            AddLocalization("AutoReplaceSettings", "自動置換設定", "Auto-Replace Settings");
            AddLocalization("AnimatorControllerReplace", "AnimatorController置換", "Replace AnimatorController");
            AddLocalization("CopyAndReplace", "コピーして置換", "Copy and Replace");
            AddLocalization("Clear", "クリア", "Clear");
            AddLocalization("PrefabReplace", "Prefab置換", "Replace Prefab");
            AddLocalization("PrefabVariantReplace", "PrefabVariantで置換", "Replace with Prefab Variant");
            AddLocalization("SetAnimatorController", "※AnimatorControllerを設定してください", "※Please set an AnimatorController");
            
            AddLocalization("AddAnimation", "アニメーションを追加", "Add Animation");
            AddLocalization("AddFromControllers", "AnimatorControllerから追加", "Add from AnimatorControllers");
            AddLocalization("DragDropAnimation", "アニメーションクリップをここにドラッグ＆ドロップ", "Drag & Drop Animation Clips Here");
            AddLocalization("DeleteAll", "すべて削除", "Delete All");
            AddLocalization("SelectAll", "すべて選択", "Select All");
            AddLocalization("UnselectAll", "すべて解除", "Unselect All");
            
            AddLocalization("GetCurrentValues", "現在値を取得", "Get Current Values");
            AddLocalization("OutputSettings", "出力設定", "Output Settings");
            AddLocalization("OutputFolder", "出力先フォルダ:", "Output Folder:");
            AddLocalization("OutputPrefix", "出力ファイル接頭辞:", "Output File Prefix:");
            AddLocalization("CapturedBlendShapes", "取得したBlendShape値", "Captured BlendShape Values");
            
            AddLocalization("GenerateAnimations", "調整アニメーション生成", "Generate Adjusted Animations");
            
            AddLocalization("Error", "エラー", "Error");
            AddLocalization("NoMeshSelected", "対象メッシュが設定されていません。", "No target mesh is selected.");
            AddLocalization("Complete", "完了", "Complete");
            AddLocalization("BlendShapeCaptured", "{0}個の値が設定されたBlendShapeの現在値を取得しました。", 
                            "Captured current values of {0} BlendShapes with non-zero values.");
            AddLocalization("Warning", "警告", "Warning");
            AddLocalization("FileExists", "「{0}」という名前のアニメーションが既に存在します。上書きしますか？", 
                           "An animation named '{0}' already exists. Do you want to overwrite it?");
            AddLocalization("Yes", "はい", "Yes");
            AddLocalization("No", "いいえ", "No");
            AddLocalization("Success", "成功", "Success");
            AddLocalization("ResultMessage", "{0}個の調整アニメーションを生成しました\n出力先: {1}", 
                            "Generated {0} adjusted animations\nOutput location: {1}");
            AddLocalization("ControllersAdjusted", "{0}個のAnimatorControllerをコピー・調整しました", 
                            "Copied and adjusted {0} AnimatorControllers");
            AddLocalization("PrefabsAdjusted", "{0}個のPrefabをコピー・調整しました", 
                            "Copied and adjusted {0} Prefabs");
            AddLocalization("PrefabVariantsCreated", "{0}個のPrefab Variantを生成・調整しました", 
                            "Created and adjusted {0} Prefab Variants");
            AddLocalization("OK", "OK", "OK");
            AddLocalization("Notice", "お知らせ", "Notice");
            AddLocalization("NoActiveBlendShapes", "このメッシュには値が設定された（0より大きい値の）BlendShapeが見つかりませんでした。\nアニメーションは変更せずにコピーされます。", 
                            "No BlendShapes with non-zero values found in this mesh.\nAnimations will be copied without changes.");
            AddLocalization("NoBlendShapes", "このメッシュにはBlendShapeがありません。\nアニメーションは変更せずにコピーされます。", 
                            "This mesh has no BlendShapes.\nAnimations will be copied without changes.");
            AddLocalization("NoBlendShapesMessage", "値が設定された（0より大きい値の）BlendShapeがこのメッシュで検出されませんでした。\nアニメーションは変更なしでコピーされます。", 
                            "No BlendShapes with non-zero values detected in this mesh.\nAnimations will be copied without modification.");
            AddLocalization("AnimationsAdded", "{0}個のアニメーションが追加されました", "{0} animations have been added");
            AddLocalization("NoAnimationsFound", "AnimatorControllerからアニメーションが見つかりませんでした", "No animations found in AnimatorControllers");
            AddLocalization("NoControllersSet", "AnimatorControllerが設定されていません", "No AnimatorControllers are set");
            AddLocalization("DuplicateAnimations", "アニメーションは追加されませんでした。重複している可能性があります。", "No animations were added. They may be duplicates.");
            
            // Additional localizations
            AddLocalization("CopyingController", "コントローラーのコピー中にエラーが発生しました", "Error copying controller");
            AddLocalization("NoControllerSelected", "AnimatorControllerが選択されていません", "No AnimatorController selected");
            AddLocalization("NoPrefabSelected", "Prefabが選択されていません", "No Prefab selected");

            // 表情アニメーション参照
            AddLocalization("ReferenceAnimations", "表情アニメーション参照", "Reference Expression Animations");
            AddLocalization("ReferenceAnimationsDesc", "BlendShape抽出用の表情アニメーション", "Expression animations for BlendShape extraction");
            AddLocalization("ExtractBlendShapes", "BlendShape抽出", "Extract BlendShapes");
            AddLocalization("AutoDetectFromAvatar", "アバターから自動取得", "Auto-Detect from Avatar");

            // 片目アニメーション生成
            AddLocalization("GenerateEyeAnimations", "片目アニメーション生成", "Generate Eye Animations");
            AddLocalization("BaseAnimation", "ベースアニメーション", "Base Animation");
            AddLocalization("GenerateLeftEye", "左目版生成", "Generate Left Eye");
            AddLocalization("GenerateRightEye", "右目版生成", "Generate Right Eye");
            AddLocalization("GenerateBothEyes", "両目版生成", "Generate Both Eyes");

            // 手動選択
            AddLocalization("ManualBlendShapeSelection", "BlendShape手動選択", "Manual BlendShape Selection");
            AddLocalization("ShowLROnly", "L/Rパターンのみ", "L/R Pattern Only");
            AddLocalization("ShowAll", "すべて表示", "Show All");
            AddLocalization("AddSelected", "選択したものを追加", "Add Selected");

            // 自動取得関連メッセージ
            AddLocalization("AvatarNotFound", "アバターのルートオブジェクトが見つかりません。", "Avatar root object not found.");
            AddLocalization("NoExpressionAnimationsFound", "表情アニメーションが見つかりませんでした。", "No expression animations found.");
            AddLocalization("ExpressionAnimationsDetected", "{0}個の表情アニメーションを検出しました。", "Detected {0} expression animations.");

            // その他のメッセージ
            AddLocalization("BlendShapesExtracted", "{0}個のBlendShapeを抽出しました", "Extracted {0} BlendShapes");
            AddLocalization("NoReferenceAnimations", "表情アニメーションが設定されていません", "No reference animations are set");
            AddLocalization("NoBlendShapesInAnimations", "表情アニメーションにBlendShapeが含まれていませんでした", "No BlendShapes found in expression animations");
            AddLocalization("EyeAnimationGenerated", "{0}の片目アニメーションを生成しました", "Generated eye animation for {0}");
            AddLocalization("SelectBaseAnimationFirst", "ベースアニメーションを選択してください", "Please select a base animation");
            AddLocalization("SetBlendShapesFirst", "先に「通常オプティマイズ」タブでBlendShapeを設定してください。", "Please set BlendShapes in the 'Main Optimize' tab first.");

            // タブ名
            AddLocalization("MainOptimizeTab", "通常オプティマイズ", "Main Optimize");
            AddLocalization("EyeAnimationTab", "片目アニメーション", "Eye Animation");
        }

        private static void AddLocalization(string key, string japanese, string english)
        {
            localizedStrings[key] = new Dictionary<Language, string>
            {
                { Language.Japanese, japanese },
                { Language.English, english }
            };
        }

        public static string L(string key, params object[] args)
        {
            if (localizedStrings.TryGetValue(key, out Dictionary<Language, string> translations))
            {
                if (translations.TryGetValue(CurrentLanguage, out string translation))
                {
                    if (args != null && args.Length > 0)
                    {
                        return string.Format(translation, args);
                    }
                    return translation;
                }
            }
            
            return key;
        }
    }
}
