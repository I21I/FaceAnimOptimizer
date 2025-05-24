using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FaceAnimOptimizer
{
    public class FaceAnimOptimizer : EditorWindow
    {
        private SkinnedMeshRenderer targetMeshRenderer;
        
        // タブ管理
        private int currentTab = 0;
        private string[] tabNames = { "通常オプティマイズ", "片目アニメーション" };
        
        // ===== 通常オプティマイズタブ用データ =====
        private List<AnimationClip> mainSourceAnimations = new List<AnimationClip>();
        private bool[] mainAnimationSelected;
        private List<string> mainTargetBlendShapes = new List<string>();
        private Dictionary<string, BlendShapeInfo> mainBlendShapeInfos = new Dictionary<string, BlendShapeInfo>();
        private bool mainHasShownBlendShapeInfo = false;
        private string mainOutputAnimationPrefix = "";
        
        // ===== 片目アニメーションタブ用データ =====
        private List<AnimationClip> eyeReferenceAnimations = new List<AnimationClip>();
        private AnimationClip baseAnimation;
        private List<string> eyeTargetBlendShapes = new List<string>();
        private Dictionary<string, BlendShapeInfo> eyeBlendShapeInfos = new Dictionary<string, BlendShapeInfo>();
        private bool eyeHasShownBlendShapeInfo = false;
        private string eyeOutputAnimationPrefix = "";
        
        // 共通データ
        private Vector2 scrollPosition;
        private Vector2 mainAnimScrollPosition;
        private Vector2 eyeAnimScrollPosition;
        private Vector2 referenceScrollPosition;
        private Vector2 controllerScrollPosition;
        private Vector2 prefabScrollPosition;
        private Vector2 mainBlendShapeScrollPosition;
        private Vector2 eyeBlendShapeScrollPosition;
        private Vector2 manualSelectionScrollPosition;
        
        private string baseOutputPath = "Assets/21CSXtools/FaceAnimOptimizer";
        private string outputFolderName = "";
        
        private Language currentLanguage = Language.Japanese;
        
        private bool autoReplaceSettingsFoldout = true;
        private bool manualSelectionFoldout = false;
        
        private GUIStyle dropAreaStyle;
        private GUIStyle dropAreaTextStyle;
        private GUIStyle thinSeparatorStyle;
        
        // 手動選択用（共通）
        private List<string> availableBlendShapes = new List<string>();
        private Dictionary<string, bool> manualSelectionStates = new Dictionary<string, bool>();
        private bool showLRPatternOnly = false;
        
        [System.Serializable]
        private class BlendShapeInfo
        {
            public float value = 0f;
            public bool selected = true;
        }
        
        private List<AnimatorController> targetAnimatorControllers = new List<AnimatorController>();
        private bool copyController = true;
        
        private List<GameObject> targetPrefabs = new List<GameObject>();
        private bool createVariant = true;
        
        private enum EyeType
        {
            LeftOnly,
            RightOnly,
            Both
        }
        
        [MenuItem("21CSX/FaceAnim Optimizer")]
        public static void ShowWindow()
        {
            GetWindow<FaceAnimOptimizer>("FaceAnim Optimizer");
        }
        
        void OnEnable()
        {
            FaceAnimOptimizerLocalization.Initialize();
            
            dropAreaStyle = new GUIStyle();
            dropAreaStyle.normal.background = FaceAnimOptimizerUtils.CreateSimpleBorderTexture();
            dropAreaStyle.border = new RectOffset(3, 3, 3, 3);
            dropAreaStyle.margin = new RectOffset(5, 5, 8, 8);
            dropAreaStyle.padding = new RectOffset(5, 5, 5, 5);
            dropAreaStyle.alignment = TextAnchor.MiddleCenter;
            
            dropAreaTextStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            
            thinSeparatorStyle = new GUIStyle();
            thinSeparatorStyle.normal.background = EditorGUIUtility.whiteTexture;
            thinSeparatorStyle.margin = new RectOffset(0, 0, 2, 2);
            thinSeparatorStyle.fixedHeight = 1;
        }
        
        private void DrawThinSeparator()
        {
            Color color = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.5f, 0.5f, 1.0f) : new Color(0.3f, 0.3f, 0.3f, 1.0f);
            
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, thinSeparatorStyle);
            if (Event.current.type == EventType.Repaint)
            {
                Color old = GUI.color;
                GUI.color = color;
                thinSeparatorStyle.Draw(rect, false, false, false, false);
                GUI.color = old;
            }
        }
        
        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // ヘッダー
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(FaceAnimOptimizerLocalization.L("WindowTitle"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            EditorGUI.BeginChangeCheck();
            currentLanguage = (Language)EditorGUILayout.EnumPopup(currentLanguage, GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
            {
                FaceAnimOptimizerLocalization.CurrentLanguage = currentLanguage;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            // 対象メッシュ（共通）
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("TargetMesh"), EditorStyles.boldLabel, GUILayout.Width(100));
            SkinnedMeshRenderer previousMeshRenderer = targetMeshRenderer;
            targetMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                targetMeshRenderer, typeof(SkinnedMeshRenderer), true, GUILayout.ExpandWidth(true));
                
            if (previousMeshRenderer != targetMeshRenderer)
            {
                mainHasShownBlendShapeInfo = false;
                mainBlendShapeInfos.Clear();
                eyeHasShownBlendShapeInfo = false;
                eyeBlendShapeInfos.Clear();
                UpdateAvailableBlendShapes();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // タブUI
            currentTab = GUILayout.Toolbar(currentTab, tabNames);
            
            EditorGUILayout.Space(3);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            switch (currentTab)
            {
                case 0:
                    DrawMainOptimizeTab();
                    break;
                case 1:
                    DrawEyeAnimationTab();
                    break;
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawMainOptimizeTab()
        {
            // 自動置換設定
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            autoReplaceSettingsFoldout = EditorGUILayout.Foldout(autoReplaceSettingsFoldout, FaceAnimOptimizerLocalization.L("AutoReplaceSettings"), true);
            
            if (autoReplaceSettingsFoldout)
            {
                DrawAutoReplaceSettings();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // 調節対象アニメーション（通常版）
            DrawMainSourceAnimationSection();
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // BlendShape取得・設定（通常版）
            DrawMainBlendShapeSection();
            
            EditorGUILayout.Space(3);
            
            // 出力設定（通常版）
            DrawMainOutputSettings();
            
            EditorGUILayout.Space(3);
            
            // メイン機能ボタン
            GUI.enabled = CanGenerateMainAnimations() && mainHasShownBlendShapeInfo;
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateAnimations"), GUILayout.Height(30)))
            {
                GenerateMainAdjustedAnimations();
            }
            GUI.enabled = true;
        }
        
        private void DrawEyeAnimationTab()
        {
            EditorGUILayout.LabelField("片目アニメーション生成", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // 表情アニメーション参照エリア（片目版専用）
            DrawEyeReferenceAnimationSection();
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // ベースアニメーション選択
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("BaseAnimation"), EditorStyles.boldLabel);
            baseAnimation = (AnimationClip)EditorGUILayout.ObjectField(
                baseAnimation, typeof(AnimationClip), false, GUILayout.ExpandWidth(true));
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // BlendShape取得・設定（片目版）
            DrawEyeBlendShapeSection();
            
            EditorGUILayout.Space(3);
            
            // 出力設定（片目版）
            DrawEyeOutputSettings();
            
            EditorGUILayout.Space(5);
            
            // 生成ボタン
            GUI.enabled = baseAnimation != null && eyeHasShownBlendShapeInfo && eyeBlendShapeInfos.Count > 0;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateLeftEye")))
            {
                GenerateEyeAnimation(EyeType.LeftOnly);
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateRightEye")))
            {
                GenerateEyeAnimation(EyeType.RightOnly);
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateBothEyes")))
            {
                GenerateEyeAnimation(EyeType.Both);
            }
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
            
            EditorGUILayout.Space(10);
            
            // 説明
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("使用方法:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 上の表情アニメーション参照でBlendShapeを抽出");
            EditorGUILayout.LabelField("2. ベースとなるまばたきアニメーションを選択");
            EditorGUILayout.LabelField("3. 左目版・右目版・両目版のボタンを押す");
            EditorGUILayout.LabelField("4. 設定されたBlendShape値が最低値として保証される");
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEyeReferenceAnimationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("ReferenceAnimations"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("ReferenceAnimationsDesc"), EditorStyles.miniLabel);
            
            // 自動取得ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("アバターから自動取得"))
            {
                AutoDetectExpressionAnimationsForEye();
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("Clear")))
            {
                eyeReferenceAnimations.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            // ドラッグ&ドロップエリア
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 30.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "", dropAreaStyle);
            EditorGUI.LabelField(dropArea, "表情アニメーションをここにドラッグ＆ドロップ", dropAreaTextStyle);
            HandleEyeReferenceAnimationDragAndDrop(dropArea);
            
            // 抽出ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("ExtractBlendShapes")))
            {
                ExtractBlendShapesFromEyeReferenceAnimations();
            }
            EditorGUILayout.EndHorizontal();
            
            // 参照アニメーション一覧
            if (eyeReferenceAnimations.Count > 0)
            {
                referenceScrollPosition = EditorGUILayout.BeginScrollView(
                    referenceScrollPosition, GUILayout.Height(80), GUILayout.ExpandWidth(true));
                
                for (int i = 0; i < eyeReferenceAnimations.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(eyeReferenceAnimations[i], typeof(AnimationClip), false);
                    
                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        eyeReferenceAnimations.RemoveAt(i);
                        GUIUtility.ExitGUI();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // === 正確なVRCアバター表情アニメーション自動取得 ===
        private void AutoDetectExpressionAnimationsForEye()
        {
            if (targetMeshRenderer == null)
            {
                EditorUtility.DisplayDialog("エラー", "対象メッシュが設定されていません。", "OK");
                return;
            }
            
            Debug.Log("=== VRCアバター表情アニメーション自動取得開始 ===");
            Debug.Log($"対象メッシュ: {targetMeshRenderer.name}");
            
            // 1. 対象メッシュからVRC Avatar Descriptorを検索
            GameObject avatarRoot = FindVRCAvatarDescriptor(targetMeshRenderer);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog("エラー", "VRC Avatar Descriptorが見つかりません。", "OK");
                return;
            }
            
            Debug.Log($"VRC Avatar Descriptor発見: {avatarRoot.name}");
            
            // 2. Avatar DescriptorからFX Controllerを取得
            AnimatorController fxController = GetFXController(avatarRoot);
            if (fxController == null)
            {
                EditorUtility.DisplayDialog("エラー", "FX Controllerが見つかりません。", "OK");
                return;
            }
            
            Debug.Log($"FX Controller発見: {fxController.name}");
            
            // 3. GestureLeft/GestureRightトランジションを持つアニメーションを検索
            List<AnimationClip> gestureAnimations = FindGestureAnimations(fxController);
            Debug.Log($"Gestureアニメーション発見: {gestureAnimations.Count}個");
            
            // 4. 対象メッシュのBlendShapeリストを取得
            List<string> meshBlendShapes = GetMeshBlendShapeNames();
            Debug.Log($"対象メッシュBlendShape数: {meshBlendShapes.Count}");
            
            // 5. L/R要素を含むBlendShapeが使用されているアニメーションをフィルタ
            List<AnimationClip> validAnimations = FilterAnimationsWithLRBlendShapes(gestureAnimations, meshBlendShapes);
            Debug.Log($"L/R BlendShape使用アニメーション: {validAnimations.Count}個");
            
            // 6. 結果をeyeReferenceAnimationsに追加
            int addedCount = 0;
            foreach (var clip in validAnimations)
            {
                if (clip != null && !eyeReferenceAnimations.Contains(clip))
                {
                    eyeReferenceAnimations.Add(clip);
                    addedCount++;
                    Debug.Log($"追加アニメーション: {clip.name}");
                }
            }
            
            Debug.Log($"=== 検出完了: 追加 {addedCount} 個 ===");
            
            if (addedCount > 0)
            {
                EditorUtility.DisplayDialog("完了", $"{addedCount}個の表情アニメーションを検出しました。", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("お知らせ", "条件に合う表情アニメーションが見つかりませんでした。", "OK");
            }
        }
        
        // 対象メッシュから上にたどってVRC Avatar Descriptorを見つける
        private GameObject FindVRCAvatarDescriptor(SkinnedMeshRenderer renderer)
        {
            Transform current = renderer.transform;
            
            while (current != null)
            {
                Debug.Log($"チェック中: {current.name}");
                
                // VRC Avatar Descriptorコンポーネントを検索
                var components = current.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "VRCAvatarDescriptor")
                    {
                        Debug.Log($"VRC Avatar Descriptor発見: {current.name}");
                        return current.gameObject;
                    }
                }
                current = current.parent;
            }
            
            Debug.Log("VRC Avatar Descriptorが見つかりませんでした");
            return null;
        }
        
        // Avatar DescriptorからFX Controllerを取得
// Avatar DescriptorからFX Controllerを取得
private AnimatorController GetFXController(GameObject avatarRoot)
{
    var avatarDescriptor = avatarRoot.GetComponent<Component>();
    if (avatarDescriptor == null) return null;
    
    // リフレクションでPlayableLayersプロパティにアクセス
    var descriptorType = avatarDescriptor.GetType();
    var playableLayersField = descriptorType.GetField("baseAnimationLayers");
    
    if (playableLayersField != null)
    {
        var playableLayers = playableLayersField.GetValue(avatarDescriptor) as System.Array;
        if (playableLayers != null)
        {
            foreach (var layer in playableLayers)
            {
                var layerType = layer.GetType();
                var typeField = layerType.GetField("type");
                var controllerField = layerType.GetField("animatorController");
                
                if (typeField != null && controllerField != null)
                {
                    var layerTypeValue = typeField.GetValue(layer);
                    
                    // FXレイヤー（通常はタイプ4）をチェック
                    if (layerTypeValue.ToString().Contains("FX") || layerTypeValue.ToString() == "4")
                    {
                        var runtimeController = controllerField.GetValue(layer) as RuntimeAnimatorController;
                        if (runtimeController is AnimatorController animController)
                        {
                            Debug.Log($"FXレイヤー発見: {animController.name}");
                            return animController;
                        }
                    }
                }
            }
        }
    }
    
    // フォールバック: Animatorコンポーネントから取得
    var animator = avatarRoot.GetComponent<Animator>();
    if (animator != null && animator.runtimeAnimatorController is AnimatorController fallbackController)
    {
        Debug.Log($"フォールバック: Animatorから取得 {fallbackController.name}");
        return fallbackController;
    }
    
    return null;
}
        
        // GestureLeft/GestureRightトランジションを持つアニメーションを検索
        private List<AnimationClip> FindGestureAnimations(AnimatorController controller)
        {
            List<AnimationClip> animations = new List<AnimationClip>();
            
            foreach (var layer in controller.layers)
            {
                Debug.Log($"レイヤー検索: {layer.name}");
                SearchStateMachineForGestureAnimations(layer.stateMachine, animations);
            }
            
            return animations;
        }
        
        private void SearchStateMachineForGestureAnimations(AnimatorStateMachine stateMachine, List<AnimationClip> animations)
        {
            // 各ステートをチェック
            foreach (var childState in stateMachine.states)
            {
                var state = childState.state;
                Debug.Log($"ステート検索: {state.name}");
                
                // このステートへのトランジションをチェック
                bool hasGestureTransition = false;
                
                // 他のステートからこのステートへのトランジションをチェック
                foreach (var otherChildState in stateMachine.states)
                {
                    foreach (var transition in otherChildState.state.transitions)
                    {
                        if (transition.destinationState == state)
                        {
                            // トランジションの条件をチェック
                            foreach (var condition in transition.conditions)
                            {
                                string paramName = condition.parameter.ToLower();
                                Debug.Log($"  トランジション条件: {condition.parameter}");
                                
                                if (paramName.Contains("gestureleft") || paramName.Contains("gestureright"))
                                {
                                    hasGestureTransition = true;
                                    Debug.Log($"    Gestureトランジション発見!");
                                    break;
                                }
                            }
                            if (hasGestureTransition) break;
                        }
                    }
                    if (hasGestureTransition) break;
                }
                
                // AnyStateからのトランジションもチェック
                foreach (var transition in stateMachine.anyStateTransitions)
                {
                    if (transition.destinationState == state)
                    {
                        foreach (var condition in transition.conditions)
                        {
                            string paramName = condition.parameter.ToLower();
                            if (paramName.Contains("gestureleft") || paramName.Contains("gestureright"))
                            {
                                hasGestureTransition = true;
                                Debug.Log($"AnyStateからGestureトランジション発見: {condition.parameter}");
                                break;
                            }
                        }
                    }
                    if (hasGestureTransition) break;
                }
                
                // Gestureトランジションがある場合、アニメーションを取得
                if (hasGestureTransition)
                {
                    if (state.motion is AnimationClip clip)
                    {
                        animations.Add(clip);
                        Debug.Log($"Gestureアニメーション追加: {clip.name}");
                    }
                    else if (state.motion is BlendTree blendTree)
                    {
                        CollectAnimationsFromBlendTree(blendTree, animations);
                    }
                }
            }
            
            // サブステートマシンも再帰的に検索
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                SearchStateMachineForGestureAnimations(childStateMachine.stateMachine, animations);
            }
        }
        
        private void CollectAnimationsFromBlendTree(BlendTree blendTree, List<AnimationClip> animations)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    animations.Add(clip);
                    Debug.Log($"BlendTreeからアニメーション追加: {clip.name}");
                }
                else if (child.motion is BlendTree subTree)
                {
                    CollectAnimationsFromBlendTree(subTree, animations);
                }
            }
        }
        
        // 対象メッシュのBlendShape名リストを取得
        private List<string> GetMeshBlendShapeNames()
        {
            List<string> blendShapeNames = new List<string>();
            
            if (targetMeshRenderer != null && targetMeshRenderer.sharedMesh != null)
            {
                Mesh mesh = targetMeshRenderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    blendShapeNames.Add(mesh.GetBlendShapeName(i));
                }
            }
            
            return blendShapeNames;
        }
        
        // L/R要素を含むBlendShapeが使用されているアニメーションをフィルタ
        private List<AnimationClip> FilterAnimationsWithLRBlendShapes(List<AnimationClip> animations, List<string> meshBlendShapes)
        {
            List<AnimationClip> validAnimations = new List<AnimationClip>();
            
            foreach (var animation in animations)
            {
                if (animation == null) continue;
                
                // アニメーションからBlendShapeを抽出
                HashSet<string> animBlendShapes = FaceAnimOptimizerUtils.ExtractBlendShapesFromAnimation(animation);
                
                Debug.Log($"アニメーション '{animation.name}' のBlendShape: {animBlendShapes.Count}個");
                
                // 対象メッシュに存在し、L/R要素を含むBlendShapeがあるかチェック
                bool hasValidLRBlendShape = false;
                
                foreach (string animBlendShape in animBlendShapes)
                {
                    // 対象メッシュに存在するかチェック
                    if (meshBlendShapes.Contains(animBlendShape))
                    {
                        // L/R要素を含むかチェック
                        if (FaceAnimOptimizerUtils.IsLeftEyeBlendShape(animBlendShape) || 
                            FaceAnimOptimizerUtils.IsRightEyeBlendShape(animBlendShape))
                        {
                            hasValidLRBlendShape = true;
                            Debug.Log($"  有効なL/R BlendShape発見: {animBlendShape}");
                            break;
                        }
                    }
                }
                
                if (hasValidLRBlendShape)
                {
                    validAnimations.Add(animation);
                    Debug.Log($"有効なアニメーション: {animation.name}");
                }
            }
            
            return validAnimations;
        }
        
        // BlendShape抽出メソッドの修正版（アニメーションからL/RのBlendShapeを抽出）
        private void ExtractBlendShapesFromEyeReferenceAnimations()
        {
            if (eyeReferenceAnimations.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    FaceAnimOptimizerLocalization.L("NoReferenceAnimations"),
                    FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Error"), FaceAnimOptimizerLocalization.L("NoMeshSelected"), FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            Debug.Log("=== BlendShape抽出開始 ===");
            
            // 参照アニメーションからBlendShapeを抽出
            HashSet<string> extractedBlendShapes = FaceAnimOptimizerUtils.ExtractBlendShapesFromAnimations(eyeReferenceAnimations);
            Debug.Log($"アニメーションから抽出されたBlendShape: {extractedBlendShapes.Count}個");
            
            if (extractedBlendShapes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    FaceAnimOptimizerLocalization.L("NoBlendShapesInAnimations"),
                    FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            eyeBlendShapeInfos.Clear();
            eyeTargetBlendShapes.Clear();
            
            // 対象メッシュのBlendShapeリストを取得
            List<string> meshBlendShapes = GetMeshBlendShapeNames();
            
            foreach (string shapeName in extractedBlendShapes)
            {
                // 対象メッシュに存在するかチェック
                int shapeIndex = targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName);
                if (shapeIndex >= 0)
                {
                    // L/R要素を含むかチェック
                    if (FaceAnimOptimizerUtils.IsLeftEyeBlendShape(shapeName) || 
                        FaceAnimOptimizerUtils.IsRightEyeBlendShape(shapeName))
                    {
                        // 現在の値を取得（0でも登録）
                        float value = targetMeshRenderer.GetBlendShapeWeight(shapeIndex);
                        
                        eyeTargetBlendShapes.Add(shapeName);
                        eyeBlendShapeInfos[shapeName] = new BlendShapeInfo { value = value, selected = true };
                        
                        Debug.Log($"L/R BlendShape登録: {shapeName} = {value:F1}%");
                    }
                }
            }
            
            if (eyeTargetBlendShapes.Count > 0)
            {
                eyeHasShownBlendShapeInfo = true;
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Complete"),
                    FaceAnimOptimizerLocalization.L("BlendShapesExtracted", eyeTargetBlendShapes.Count),
                    FaceAnimOptimizerLocalization.L("OK"));
            }
            else
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    "L/R要素を含むBlendShapeが見つかりませんでした。",
                    FaceAnimOptimizerLocalization.L("OK"));
            }
            
            Debug.Log("=== BlendShape抽出完了 ===");
        }
        
        // 以下は元のコードと同じメソッド群...
        
        private void DrawMainSourceAnimationSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(FaceAnimOptimizerLocalization.L("AddAnimation"), EditorStyles.boldLabel);
            
            GUILayout.Space(20);
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("AddFromControllers"), GUILayout.Width(180)))
            {
                AddMainAnimationsFromControllers();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 40.0f, GUILayout.Width(Screen.width - 15), GUILayout.MaxWidth(Screen.width - 15));
            dropArea.x += 15;
            GUI.Box(dropArea, "", dropAreaStyle);
            
            EditorGUI.LabelField(dropArea, FaceAnimOptimizerLocalization.L("DragDropAnimation"), dropAreaTextStyle);
            
            HandleMainDragAndDrop(dropArea);
            
            if (mainSourceAnimations.Count > 0)
            {
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button(FaceAnimOptimizerLocalization.L("DeleteAll")))
                {
                    mainSourceAnimations.Clear();
                    mainAnimationSelected = null;
                }
            }
            
            DisplayMainAnimationsList();
        }
        
        private void DrawMainBlendShapeSection()
        {
            // BlendShape取得ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GetCurrentValues"), GUILayout.Height(30)))
            {
                CaptureMainCurrentBlendShapeValues();
                mainHasShownBlendShapeInfo = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            // 手動選択UI
            DrawMainManualSelectionUI();
            
            // BlendShape値表示
            if (targetMeshRenderer != null && mainHasShownBlendShapeInfo)
            {
                if (mainBlendShapeInfos.Count > 0)
                {
                    DisplayMainBlendShapeValues();
                }
                else
                {
                    EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                }
            }
        }
        
        private void DrawEyeBlendShapeSection()
        {
            // BlendShape取得ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GetCurrentValues"), GUILayout.Height(30)))
            {
                CaptureEyeCurrentBlendShapeValues();
                eyeHasShownBlendShapeInfo = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            // 手動選択UI
            DrawEyeManualSelectionUI();
            
            // BlendShape値表示
            if (targetMeshRenderer != null && eyeHasShownBlendShapeInfo)
            {
                if (eyeBlendShapeInfos.Count > 0)
                {
                    DisplayEyeBlendShapeValues();
                }
                else
                {
                    EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                }
            }
        }
        
        private void DrawMainManualSelectionUI()
        {
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
                return;
                
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            manualSelectionFoldout = EditorGUILayout.Foldout(manualSelectionFoldout, FaceAnimOptimizerLocalization.L("ManualBlendShapeSelection"), true);
            
            if (manualSelectionFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(showLRPatternOnly ? FaceAnimOptimizerLocalization.L("ShowAll") : FaceAnimOptimizerLocalization.L("ShowLROnly")))
                {
                    showLRPatternOnly = !showLRPatternOnly;
                }
                
                if (GUILayout.Button(FaceAnimOptimizerLocalization.L("AddSelected")))
                {
                    AddMainSelectedBlendShapes();
                }
                EditorGUILayout.EndHorizontal();
                
                if (availableBlendShapes.Count > 0)
                {
                    manualSelectionScrollPosition = EditorGUILayout.BeginScrollView(
                        manualSelectionScrollPosition, GUILayout.Height(120), GUILayout.ExpandWidth(true));
                    
                    var displayList = showLRPatternOnly ? 
                        FaceAnimOptimizerUtils.FilterBlendShapesByLRPattern(availableBlendShapes) : 
                        availableBlendShapes;
                    
                    foreach (var shapeName in displayList)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        bool currentState = manualSelectionStates.ContainsKey(shapeName) ? manualSelectionStates[shapeName] : false;
                        bool newState = EditorGUILayout.Toggle(currentState, GUILayout.Width(20));
                        manualSelectionStates[shapeName] = newState;
                        
                        EditorGUILayout.LabelField(shapeName);
                        
                        float currentValue = targetMeshRenderer.GetBlendShapeWeight(
                            targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName));
                        EditorGUILayout.LabelField($"{currentValue:F1}%", GUILayout.Width(50));
                        
                        // L/R判定結果表示
                        string lrType = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(shapeName) ? "[L]" : 
                                       FaceAnimOptimizerUtils.IsRightEyeBlendShape(shapeName) ? "[R]" : "";
                        if (!string.IsNullOrEmpty(lrType))
                        {
                            EditorGUILayout.LabelField(lrType, GUILayout.Width(25));
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEyeManualSelectionUI()
        {
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
                return;
                
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool eyeManualSelectionFoldout = EditorGUILayout.Foldout(manualSelectionFoldout, FaceAnimOptimizerLocalization.L("ManualBlendShapeSelection"), true);
            
            if (eyeManualSelectionFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(showLRPatternOnly ? FaceAnimOptimizerLocalization.L("ShowAll") : FaceAnimOptimizerLocalization.L("ShowLROnly")))
                {
                    showLRPatternOnly = !showLRPatternOnly;
                }
                
                if (GUILayout.Button(FaceAnimOptimizerLocalization.L("AddSelected")))
                {
                    AddEyeSelectedBlendShapes();
                }
                EditorGUILayout.EndHorizontal();
                
                if (availableBlendShapes.Count > 0)
                {
                    manualSelectionScrollPosition = EditorGUILayout.BeginScrollView(
                        manualSelectionScrollPosition, GUILayout.Height(120), GUILayout.ExpandWidth(true));
                    
                    var displayList = showLRPatternOnly ? 
                        FaceAnimOptimizerUtils.FilterBlendShapesByLRPattern(availableBlendShapes) : 
                        availableBlendShapes;
                    
                    foreach (var shapeName in displayList)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        bool currentState = manualSelectionStates.ContainsKey(shapeName) ? manualSelectionStates[shapeName] : false;
                        bool newState = EditorGUILayout.Toggle(currentState, GUILayout.Width(20));
                        manualSelectionStates[shapeName] = newState;
                        
                        EditorGUILayout.LabelField(shapeName);
                        
                        float currentValue = targetMeshRenderer.GetBlendShapeWeight(
                            targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName));
                        EditorGUILayout.LabelField($"{currentValue:F1}%", GUILayout.Width(50));
                        
                        // L/R判定結果表示
                        string lrType = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(shapeName) ? "[L]" : 
                                       FaceAnimOptimizerUtils.IsRightEyeBlendShape(shapeName) ? "[R]" : "";
                        if (!string.IsNullOrEmpty(lrType))
                        {
                            EditorGUILayout.LabelField(lrType, GUILayout.Width(25));
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void UpdateAvailableBlendShapes()
        {
            availableBlendShapes.Clear();
            manualSelectionStates.Clear();
            
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
                return;
            
            Mesh mesh = targetMeshRenderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                availableBlendShapes.Add(name);
                manualSelectionStates[name] = false;
            }
        }
        
        private void AddMainSelectedBlendShapes()
        {
            mainBlendShapeInfos.Clear();
            mainTargetBlendShapes.Clear();
            
            bool foundSelected = false;
            
            foreach (var kvp in manualSelectionStates)
            {
                if (kvp.Value) // 選択されている
                {
                    string shapeName = kvp.Key;
                    float value = targetMeshRenderer.GetBlendShapeWeight(
                        targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName));
                    
                    if (value > 0.001f)
                    {
                        mainTargetBlendShapes.Add(shapeName);
                        mainBlendShapeInfos[shapeName] = new BlendShapeInfo { value = value, selected = true };
                        foundSelected = true;
                    }
                }
            }
            
            if (foundSelected)
            {
                mainHasShownBlendShapeInfo = true;
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Complete"),
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", mainTargetBlendShapes.Count),
                    FaceAnimOptimizerLocalization.L("OK"));
            }
            else
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    FaceAnimOptimizerLocalization.L("NoActiveBlendShapes"),
                    FaceAnimOptimizerLocalization.L("OK"));
            }
        }
        
        private void AddEyeSelectedBlendShapes()
        {
            eyeBlendShapeInfos.Clear();
            eyeTargetBlendShapes.Clear();
            
            bool foundSelected = false;
            
            foreach (var kvp in manualSelectionStates)
            {
                if (kvp.Value) // 選択されている
                {
                    string shapeName = kvp.Key;
                    float value = targetMeshRenderer.GetBlendShapeWeight(
                        targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName));
                    
                    if (value > 0.001f)
                    {
                        eyeTargetBlendShapes.Add(shapeName);
                        eyeBlendShapeInfos[shapeName] = new BlendShapeInfo { value = value, selected = true };
                        foundSelected = true;
                    }
                }
            }
            
            if (foundSelected)
            {
                eyeHasShownBlendShapeInfo = true;
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Complete"),
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", eyeTargetBlendShapes.Count),
                    FaceAnimOptimizerLocalization.L("OK"));
            }
            else
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    FaceAnimOptimizerLocalization.L("NoActiveBlendShapes"),
                    FaceAnimOptimizerLocalization.L("OK"));
            }
        }
        
        private void HandleEyeReferenceAnimationDragAndDrop(Rect dropArea)
        {
            Event currentEvent = Event.current;
            
            if (!dropArea.Contains(currentEvent.mousePosition))
                return;
                
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    bool hasValidObjects = DragAndDrop.objectReferences.Any(obj => obj is AnimationClip);
                    DragAndDrop.visualMode = hasValidObjects ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    currentEvent.Use();
                    break;
                    
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    
                    foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is AnimationClip clip)
                        {
                            if (!eyeReferenceAnimations.Contains(clip))
                            {
                                eyeReferenceAnimations.Add(clip);
                            }
                        }
                    }
                    
                    Repaint();
                    currentEvent.Use();
                    break;
            }
        }
        
        private void GenerateEyeAnimation(EyeType eyeType)
        {
            if (baseAnimation == null)
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Error"),
                    FaceAnimOptimizerLocalization.L("SelectBaseAnimationFirst"),
                    FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            outputFolderName = "EyeAnim" + DateTime.Now.ToString("yyMMddHHmmss");
            string outputFolder = baseOutputPath + "/" + outputFolderName;
            
            CreateOutputFolder(outputFolder);
            
            string suffix = "";
            switch (eyeType)
            {
                case EyeType.LeftOnly: suffix = "_LeftEye"; break;
                case EyeType.RightOnly: suffix = "_RightEye"; break;
                case EyeType.Both: suffix = "_BothEyes"; break;
            }
            
            string newClipName = eyeOutputAnimationPrefix + baseAnimation.name + suffix;
            AnimationClip newClip = new AnimationClip();
            newClip.name = newClipName;
            newClip.frameRate = baseAnimation.frameRate;
            
            // アニメーション設定をコピー
            AnimationClipSettings sourceSettings = AnimationUtility.GetAnimationClipSettings(baseAnimation);
            AnimationUtility.SetAnimationClipSettings(newClip, sourceSettings);
            
            // カーブを調整
            CreateEyeSpecificCurves(baseAnimation, newClip, eyeType);
            
            string assetPath = $"{outputFolder}/{newClip.name}.anim";
            AssetDatabase.CreateAsset(newClip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                FaceAnimOptimizerLocalization.L("Success"),
                FaceAnimOptimizerLocalization.L("EyeAnimationGenerated", eyeType.ToString()),
                FaceAnimOptimizerLocalization.L("OK"));
            
            // 生成されたフォルダを選択
            EditorUtility.FocusProjectWindow();
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputFolder);
            if (folderAsset != null)
            {
                Selection.activeObject = folderAsset;
            }
        }
        
        private void CreateEyeSpecificCurves(AnimationClip sourceClip, AnimationClip newClip, EyeType eyeType)
        {
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(sourceClip);
            
            foreach (EditorCurveBinding binding in curveBindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                    
                    bool shouldApplyBlendShape = false;
                    BlendShapeInfo info = null;
                    
                    // 対象のBlendShapeかどうかを判定（片目版のデータを使用）
                    switch (eyeType)
                    {
                        case EyeType.LeftOnly:
                            shouldApplyBlendShape = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(blendShapeName) &&
                                                  eyeBlendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
                            break;
                        case EyeType.RightOnly:
                            shouldApplyBlendShape = FaceAnimOptimizerUtils.IsRightEyeBlendShape(blendShapeName) &&
                                                  eyeBlendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
                            break;
                        case EyeType.Both:
                            shouldApplyBlendShape = (FaceAnimOptimizerUtils.IsLeftEyeBlendShape(blendShapeName) || 
                                                   FaceAnimOptimizerUtils.IsRightEyeBlendShape(blendShapeName)) &&
                                                  eyeBlendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
                            break;
                    }
                    
                    if (shouldApplyBlendShape && info != null)
                    {
                        AnimationCurve adjustedCurve = CreateAdjustedCurve(sourceCurve, info.value);
                        AnimationUtility.SetEditorCurve(newClip, binding, adjustedCurve);
                    }
                    else
                    {
                        AnimationUtility.SetEditorCurve(newClip, binding, sourceCurve);
                    }
                }
                else
                {
                    AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                    AnimationUtility.SetEditorCurve(newClip, binding, sourceCurve);
                }
            }
        }
        
        private void CreateOutputFolder(string outputFolder)
        {
            if (!AssetDatabase.IsValidFolder(baseOutputPath))
            {
                string[] parts = baseOutputPath.Split('/');
                string currentPath = parts[0];
                
                for (int i = 1; i < parts.Length; i++)
                {
                    string newPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = newPath;
                }
            }
            
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                AssetDatabase.CreateFolder(baseOutputPath, outputFolder.Split('/').Last());
            }
        }
        
        private void DrawAutoReplaceSettings()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("AnimatorControllerReplace"), GUILayout.Width(200));

            Rect toggleRect = GUILayoutUtility.GetRect(15, 18, GUILayout.Width(15));
            copyController = EditorGUI.Toggle(toggleRect, copyController);
            EditorGUI.LabelField(new Rect(toggleRect.x + 18, toggleRect.y, 150, 18), FaceAnimOptimizerLocalization.L("CopyAndReplace"));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            AnimatorController newController = (AnimatorController)EditorGUILayout.ObjectField(
                null, typeof(AnimatorController), false, GUILayout.ExpandWidth(true));
            
            if (newController != null && !targetAnimatorControllers.Contains(newController))
            {
                targetAnimatorControllers.Add(newController);
            }
            
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("Clear"), GUILayout.Width(60)))
            {
                targetAnimatorControllers.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            if (targetAnimatorControllers.Count > 0)
            {
                DrawControllerList();
            }

            EditorGUILayout.Space();
            
            bool hasAnimatorControllers = targetAnimatorControllers.Count > 0;
            if (!hasAnimatorControllers)
            {
                GUI.enabled = false;
                EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("SetAnimatorController"), EditorStyles.miniLabel);
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("PrefabReplace"), GUILayout.Width(200));

            toggleRect = GUILayoutUtility.GetRect(15, 18, GUILayout.Width(15));
            if (!GUI.enabled && !createVariant)
            {
                createVariant = true;
            }
            createVariant = EditorGUI.Toggle(toggleRect, createVariant);
            EditorGUI.LabelField(new Rect(toggleRect.x + 18, toggleRect.y, 200, 18), FaceAnimOptimizerLocalization.L("PrefabVariantReplace"));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                null, typeof(GameObject), false, GUILayout.ExpandWidth(true));
            
            if (newPrefab != null && !targetPrefabs.Contains(newPrefab) && PrefabUtility.IsPartOfPrefabAsset(newPrefab))
            {
                targetPrefabs.Add(newPrefab);
            }
            
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("Clear"), GUILayout.Width(60)))
            {
                targetPrefabs.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            if (targetPrefabs.Count > 0)
            {
                DrawPrefabList();
            }
            
            if (!hasAnimatorControllers)
            {
                GUI.enabled = true;
            }
        }
        
        private void DrawControllerList()
        {
            controllerScrollPosition = EditorGUILayout.BeginScrollView(
                controllerScrollPosition, GUILayout.Height(50), GUILayout.ExpandWidth(true));
            
            EditorGUILayout.BeginVertical();
            for (int i = 0; i < targetAnimatorControllers.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(targetAnimatorControllers[i], typeof(AnimatorController), false);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    targetAnimatorControllers.RemoveAt(i);
                    GUIUtility.ExitGUI();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawPrefabList()
        {
            prefabScrollPosition = EditorGUILayout.BeginScrollView(
                prefabScrollPosition, GUILayout.Height(50), GUILayout.ExpandWidth(true));
            
            EditorGUILayout.BeginVertical();
            for (int i = 0; i < targetPrefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(targetPrefabs[i], typeof(GameObject), false);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    targetPrefabs.RemoveAt(i);
                    GUIUtility.ExitGUI();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawMainOutputSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputSettings"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputFolder"), GUILayout.Width(100));
            EditorGUILayout.LabelField(baseOutputPath + "/Anim[日付]");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputPrefix"), GUILayout.Width(100));
            mainOutputAnimationPrefix = EditorGUILayout.TextField(mainOutputAnimationPrefix);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEyeOutputSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputSettings"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputFolder"), GUILayout.Width(100));
            EditorGUILayout.LabelField(baseOutputPath + "/EyeAnim[日付]");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputPrefix"), GUILayout.Width(100));
            eyeOutputAnimationPrefix = EditorGUILayout.TextField(eyeOutputAnimationPrefix);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleMainDragAndDrop(Rect dropArea)
        {
            Event currentEvent = Event.current;
            
            if (!dropArea.Contains(currentEvent.mousePosition))
                return;
                
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    bool hasValidObjects = DragAndDrop.objectReferences.Any(obj => 
                        obj is AnimationClip || 
                        obj is AnimatorController || 
                        (obj is GameObject && PrefabUtility.IsPartOfPrefabAsset(obj)));
                        
                    DragAndDrop.visualMode = hasValidObjects ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    currentEvent.Use();
                    break;
                    
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    
                    foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is AnimationClip)
                        {
                            AddMainAnimationClip(draggedObject as AnimationClip);
                        }
                        else if (draggedObject is AnimatorController)
                        {
                            if (!targetAnimatorControllers.Contains(draggedObject as AnimatorController))
                            {
                                targetAnimatorControllers.Add(draggedObject as AnimatorController);
                            }
                        }
                        else if (draggedObject is GameObject && PrefabUtility.IsPartOfPrefabAsset(draggedObject))
                        {
                            if (!targetPrefabs.Contains(draggedObject as GameObject))
                            {
                                targetPrefabs.Add(draggedObject as GameObject);
                            }
                        }
                    }
                    
                    Repaint();
                    currentEvent.Use();
                    break;
            }
        }
        
        private void AddMainAnimationClip(AnimationClip clip)
        {
            if (clip != null && !mainSourceAnimations.Contains(clip))
            {
                mainSourceAnimations.Add(clip);
                
                mainSourceAnimations.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                
                bool[] newArray = new bool[mainSourceAnimations.Count];
                
                if (mainAnimationSelected != null)
                {
                    for (int i = 0; i < mainAnimationSelected.Length; i++)
                    {
                        if (i < mainSourceAnimations.Count)
                        {
                            newArray[i] = mainAnimationSelected[i];
                        }
                    }
                }
                
                for (int i = 0; i < newArray.Length; i++)
                {
                    newArray[i] = true;
                }
                
                mainAnimationSelected = newArray;
            }
        }
        
        private void DisplayMainAnimationsList()
        {
            if (mainSourceAnimations.Count == 0)
            {
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                for (int i = 0; i < mainAnimationSelected.Length; i++)
                    mainAnimationSelected[i] = true;
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                for (int i = 0; i < mainAnimationSelected.Length; i++)
                    mainAnimationSelected[i] = false;
            }
            EditorGUILayout.EndHorizontal();
            
            float listHeight = Mathf.Min(120, mainSourceAnimations.Count * 20 + 10);
            mainAnimScrollPosition = EditorGUILayout.BeginScrollView(mainAnimScrollPosition, GUILayout.Height(listHeight));
            
            for (int i = 0; i < mainSourceAnimations.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (mainAnimationSelected != null && i < mainAnimationSelected.Length)
                {
                    mainAnimationSelected[i] = EditorGUILayout.Toggle(mainAnimationSelected[i], GUILayout.Width(20));
                }
                
                EditorGUILayout.ObjectField(mainSourceAnimations[i], typeof(AnimationClip), false);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    mainSourceAnimations.RemoveAt(i);
                    
                    if (mainSourceAnimations.Count > 0 && mainAnimationSelected != null)
                    {
                        bool[] newArray = new bool[mainSourceAnimations.Count];
                        for (int j = 0; j < i && j < mainAnimationSelected.Length; j++)
                            newArray[j] = mainAnimationSelected[j];
                        for (int j = i; j < mainSourceAnimations.Count && j+1 < mainAnimationSelected.Length; j++)
                            newArray[j] = mainAnimationSelected[j + 1];
                        mainAnimationSelected = newArray;
                    }
                    else
                    {
                        mainAnimationSelected = null;
                    }
                    
                    GUIUtility.ExitGUI();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void AddMainAnimationsFromControllers()
        {
            if (targetAnimatorControllers.Count == 0)
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), FaceAnimOptimizerLocalization.L("NoControllersSet"), FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            HashSet<AnimationClip> uniqueClips = new HashSet<AnimationClip>();
            int addedCount = 0;
            int totalFoundCount = 0;
            
            foreach (AnimatorController controller in targetAnimatorControllers)
            {
                if (controller == null)
                    continue;
                    
                List<AnimationClip> clips = new List<AnimationClip>();
                FaceAnimOptimizerUtils.CollectAnimationClipsFromController(controller, clips);
                totalFoundCount += clips.Count;
                
                foreach (AnimationClip clip in clips)
                {
                    if (clip != null && !mainSourceAnimations.Contains(clip) && uniqueClips.Add(clip))
                    {
                        mainSourceAnimations.Add(clip);
                        addedCount++;
                    }
                }
            }
            
            mainSourceAnimations.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            
            bool[] newArray = new bool[mainSourceAnimations.Count];
            if (mainAnimationSelected != null)
            {
                for (int i = 0; i < Mathf.Min(mainAnimationSelected.Length, mainSourceAnimations.Count); i++)
                {
                    newArray[i] = mainAnimationSelected[i];
                }
            }
            
            for (int i = 0; i < mainSourceAnimations.Count; i++)
            {
                if (i >= mainAnimationSelected?.Length || mainAnimationSelected == null)
                {
                    newArray[i] = true;
                }
            }
            
            mainAnimationSelected = newArray;
            
            if (addedCount > 0)
            {
                string message = FaceAnimOptimizerLocalization.L("AnimationsAdded", addedCount);
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Success"), message, FaceAnimOptimizerLocalization.L("OK"));
            }
            else
            {
                if (totalFoundCount > 0)
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), FaceAnimOptimizerLocalization.L("DuplicateAnimations"), FaceAnimOptimizerLocalization.L("OK"));
                }
                else
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), FaceAnimOptimizerLocalization.L("NoAnimationsFound"), FaceAnimOptimizerLocalization.L("OK"));
                }
            }
        }
        
        private void CaptureMainCurrentBlendShapeValues()
        {
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Error"), FaceAnimOptimizerLocalization.L("NoMeshSelected"), FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            mainBlendShapeInfos.Clear();
            mainTargetBlendShapes.Clear();
            
            Mesh mesh = targetMeshRenderer.sharedMesh;
            bool foundActiveBlendShapes = false;
            
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                float value = targetMeshRenderer.GetBlendShapeWeight(i);
                
                if (value > 0.001f)
                {
                    mainTargetBlendShapes.Add(name);
                    mainBlendShapeInfos[name] = new BlendShapeInfo { value = value, selected = true };
                    foundActiveBlendShapes = true;
                }
            }
            
            if (!foundActiveBlendShapes)
            {
                if (mesh.blendShapeCount > 0)
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), 
                        FaceAnimOptimizerLocalization.L("NoActiveBlendShapes"), 
                        FaceAnimOptimizerLocalization.L("OK"));
                }
                else
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), 
                        FaceAnimOptimizerLocalization.L("NoBlendShapes"), 
                        FaceAnimOptimizerLocalization.L("OK"));
                }
            }
            else
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Complete"), 
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", mainTargetBlendShapes.Count), 
                    FaceAnimOptimizerLocalization.L("OK"));
            }
        }
        
        private void CaptureEyeCurrentBlendShapeValues()
        {
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Error"), FaceAnimOptimizerLocalization.L("NoMeshSelected"), FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            eyeBlendShapeInfos.Clear();
            eyeTargetBlendShapes.Clear();
            
            Mesh mesh = targetMeshRenderer.sharedMesh;
            bool foundActiveBlendShapes = false;
            
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                float value = targetMeshRenderer.GetBlendShapeWeight(i);
                
                if (value > 0.001f)
                {
                    eyeTargetBlendShapes.Add(name);
                    eyeBlendShapeInfos[name] = new BlendShapeInfo { value = value, selected = true };
                    foundActiveBlendShapes = true;
                }
            }
            
            if (!foundActiveBlendShapes)
            {
                if (mesh.blendShapeCount > 0)
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), 
                        FaceAnimOptimizerLocalization.L("NoActiveBlendShapes"), 
                        FaceAnimOptimizerLocalization.L("OK"));
                }
                else
                {
                    EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Notice"), 
                        FaceAnimOptimizerLocalization.L("NoBlendShapes"), 
                        FaceAnimOptimizerLocalization.L("OK"));
                }
            }
            else
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Complete"), 
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", eyeTargetBlendShapes.Count), 
                    FaceAnimOptimizerLocalization.L("OK"));
            }
        }
        
        private void DisplayMainBlendShapeValues()
        {
            if (mainTargetBlendShapes.Count == 0)
            {
                EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("CapturedBlendShapes"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"総数: {mainTargetBlendShapes.Count}個", EditorStyles.miniLabel);
            if (GUILayout.Button("デバッグ出力", GUILayout.Width(80)))
            {
                FaceAnimOptimizerUtils.DebugBlendShapeNames(mainTargetBlendShapes, "Main_DisplayBlendShapeValues");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                foreach (var name in mainTargetBlendShapes)
                {
                    if (mainBlendShapeInfos.ContainsKey(name))
                    {
                        var info = mainBlendShapeInfos[name];
                        info.selected = true;
                        mainBlendShapeInfos[name] = info;
                    }
                }
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                foreach (var name in mainTargetBlendShapes)
                {
                    if (mainBlendShapeInfos.ContainsKey(name))
                    {
                        var info = mainBlendShapeInfos[name];
                        info.selected = false;
                        mainBlendShapeInfos[name] = info;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            mainBlendShapeScrollPosition = EditorGUILayout.BeginScrollView(
                mainBlendShapeScrollPosition, GUILayout.Height(150), GUILayout.ExpandWidth(true));
                
            foreach (var name in mainTargetBlendShapes)
            {
                if (mainBlendShapeInfos.ContainsKey(name))
                {
                    var info = mainBlendShapeInfos[name];
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                    if (newSelected != info.selected)
                    {
                        info.selected = newSelected;
                        mainBlendShapeInfos[name] = info;
                    }
                    
                    EditorGUILayout.LabelField(name, GUILayout.MinWidth(100));
                    EditorGUILayout.LabelField($"{info.value:F1}%", GUILayout.Width(50));
                    
                    // L/R判定結果も表示
                    string lrType = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(name) ? "[L]" : 
                                   FaceAnimOptimizerUtils.IsRightEyeBlendShape(name) ? "[R]" : "";
                    if (!string.IsNullOrEmpty(lrType))
                    {
                        EditorGUILayout.LabelField(lrType, GUILayout.Width(25));
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DisplayEyeBlendShapeValues()
        {
            if (eyeTargetBlendShapes.Count == 0)
            {
                EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("CapturedBlendShapes"), EditorStyles.boldLabel);
            
            // L/Rパターンの確認表示
            var leftShapes = eyeTargetBlendShapes.Where(FaceAnimOptimizerUtils.IsLeftEyeBlendShape).ToList();
            var rightShapes = eyeTargetBlendShapes.Where(FaceAnimOptimizerUtils.IsRightEyeBlendShape).ToList();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"総数: {eyeTargetBlendShapes.Count}個 (左目用: {leftShapes.Count}個, 右目用: {rightShapes.Count}個)", EditorStyles.miniLabel);
            if (GUILayout.Button("デバッグ出力", GUILayout.Width(80)))
            {
                FaceAnimOptimizerUtils.DebugBlendShapeNames(eyeTargetBlendShapes, "Eye_DisplayBlendShapeValues");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                foreach (var name in eyeTargetBlendShapes)
                {
                    if (eyeBlendShapeInfos.ContainsKey(name))
                    {
                        var info = eyeBlendShapeInfos[name];
                        info.selected = true;
                        eyeBlendShapeInfos[name] = info;
                    }
                }
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                foreach (var name in eyeTargetBlendShapes)
                {
                    if (eyeBlendShapeInfos.ContainsKey(name))
                    {
                        var info = eyeBlendShapeInfos[name];
                        info.selected = false;
                        eyeBlendShapeInfos[name] = info;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            eyeBlendShapeScrollPosition = EditorGUILayout.BeginScrollView(
                eyeBlendShapeScrollPosition, GUILayout.Height(150), GUILayout.ExpandWidth(true));
                
            foreach (var name in eyeTargetBlendShapes)
            {
                if (eyeBlendShapeInfos.ContainsKey(name))
                {
                    var info = eyeBlendShapeInfos[name];
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                    if (newSelected != info.selected)
                    {
                        info.selected = newSelected;
                        eyeBlendShapeInfos[name] = info;
                    }
                    
                    EditorGUILayout.LabelField(name, GUILayout.MinWidth(100));
                    EditorGUILayout.LabelField($"{info.value:F1}%", GUILayout.Width(50));
                    
                    // L/R判定結果も表示
                    string lrType = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(name) ? "[L]" : 
                                   FaceAnimOptimizerUtils.IsRightEyeBlendShape(name) ? "[R]" : "";
                    if (!string.IsNullOrEmpty(lrType))
                    {
                        EditorGUILayout.LabelField(lrType, GUILayout.Width(25));
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private bool CanGenerateMainAnimations()
        {
            return targetMeshRenderer != null && 
                mainSourceAnimations.Count > 0 && 
                mainAnimationSelected != null;
        }
        
        private void GenerateMainAdjustedAnimations()
        {
            outputFolderName = "Anim" + DateTime.Now.ToString("yyMMddHHmmss");
            string outputFolder = baseOutputPath + "/" + outputFolderName;
            
            CreateOutputFolder(outputFolder);
            
            Dictionary<AnimationClip, AnimationClip> animReplacementMap = new Dictionary<AnimationClip, AnimationClip>();
            Dictionary<AnimatorController, AnimatorController> controllerReplacementMap = new Dictionary<AnimatorController, AnimatorController>();
            Dictionary<GameObject, GameObject> prefabReplacementMap = new Dictionary<GameObject, GameObject>();
            int processedCount = 0;
            
            for (int i = 0; i < mainSourceAnimations.Count; i++)
            {
                if (mainAnimationSelected == null || i >= mainAnimationSelected.Length || !mainAnimationSelected[i])
                    continue;
                    
                AnimationClip sourceClip = mainSourceAnimations[i];
                if (sourceClip == null)
                    continue;
                    
                string newClipName;
                if (string.IsNullOrEmpty(mainOutputAnimationPrefix))
                {
                    newClipName = "FAO_" + sourceClip.name;
                }
                else
                {
                    newClipName = mainOutputAnimationPrefix + sourceClip.name;
                }
                
                AnimationClip newClip = new AnimationClip();
                newClip.name = newClipName;
                newClip.frameRate = sourceClip.frameRate;
                
                AnimationClipSettings sourceSettings = AnimationUtility.GetAnimationClipSettings(sourceClip);
                AnimationClipSettings newSettings = new AnimationClipSettings
                {
                    loopTime = sourceSettings.loopTime,
                    loopBlend = sourceSettings.loopBlend,
                    cycleOffset = sourceSettings.cycleOffset,
                    heightFromFeet = sourceSettings.heightFromFeet,
                    keepOriginalOrientation = sourceSettings.keepOriginalOrientation,
                    keepOriginalPositionXZ = sourceSettings.keepOriginalPositionXZ,
                    keepOriginalPositionY = sourceSettings.keepOriginalPositionY,
                    mirror = sourceSettings.mirror,
                    startTime = sourceSettings.startTime,
                    stopTime = sourceSettings.stopTime
                };
                
                CreateMainAdjustedCurves(sourceClip, newClip);
                
                AnimationUtility.SetAnimationClipSettings(newClip, newSettings);
                
                string assetPath = $"{outputFolder}/{newClip.name}.anim";
                
                if (File.Exists(assetPath))
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        FaceAnimOptimizerLocalization.L("Warning"),
                        FaceAnimOptimizerLocalization.L("FileExists", newClip.name),
                        FaceAnimOptimizerLocalization.L("Yes"),
                        FaceAnimOptimizerLocalization.L("No")
                    );
                    
                    if (!overwrite)
                    {
                        int counter = 1;
                        string newPath;
                        do {
                            newPath = $"{outputFolder}/{newClip.name}_{counter}.anim";
                            counter++;
                        } while (File.Exists(newPath));
                        
                        assetPath = newPath;
                        newClip.name = Path.GetFileNameWithoutExtension(assetPath);
                    }
                    else
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                }
                
                AssetDatabase.CreateAsset(newClip, assetPath);
                animReplacementMap[sourceClip] = newClip;
                processedCount++;
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // AnimatorController処理は同じ
            if (targetAnimatorControllers.Count > 0 && animReplacementMap.Count > 0)
            {
                foreach (AnimatorController controller in targetAnimatorControllers)
                {
                    if (controller != null)
                    {
                        if (copyController)
                        {
                            try
                            {
                                AnimatorController newController = FaceAnimOptimizerUtils.CopyAnimatorController(controller, outputFolder);
                                if (newController != null)
                                {
                                    AssetDatabase.SaveAssets();
                                    AssetDatabase.Refresh();
                                    
                                    controllerReplacementMap[controller] = newController;
                                    ReplaceAnimationsInController(newController, animReplacementMap);
                                    
                                    AssetDatabase.SaveAssets();
                                    AssetDatabase.Refresh();
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"Error copying controller: {ex.Message}");
                            }
                        }
                        else
                        {
                            try
                            {
                                ReplaceAnimationsInController(controller, animReplacementMap);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"Error replacing animations: {ex.Message}");
                            }
                        }
                    }
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Prefab処理も同じ
            if (targetPrefabs.Count > 0 && controllerReplacementMap.Count > 0)
            {
                foreach (GameObject prefab in targetPrefabs)
                {
                    if (prefab != null)
                    {
                        GameObject newPrefab;
                        
                        if (createVariant)
                        {
                            newPrefab = CreatePrefabVariant(prefab, outputFolder, controllerReplacementMap);
                        }
                        else
                        {
                            newPrefab = FaceAnimOptimizerUtils.CopyPrefab(prefab, outputFolder);
                            
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            
                            ReplacePrefabControllers(newPrefab, controllerReplacementMap, animReplacementMap);
                        }
                        
                        if (newPrefab != null)
                        {
                            prefabReplacementMap[prefab] = newPrefab;
                        }
                    }
                }
            }
            
            string resultMessage = FaceAnimOptimizerLocalization.L("ResultMessage", processedCount, outputFolder);
            
            if (copyController && controllerReplacementMap.Count > 0)
            {
                resultMessage += "\n" + FaceAnimOptimizerLocalization.L("ControllersAdjusted", controllerReplacementMap.Count);
            }
            
            if (targetPrefabs.Count > 0 && prefabReplacementMap.Count > 0)
            {
                if (createVariant)
                {
                    resultMessage += "\n" + FaceAnimOptimizerLocalization.L("PrefabVariantsCreated", prefabReplacementMap.Count);
                }
                else
                {
                    resultMessage += "\n" + FaceAnimOptimizerLocalization.L("PrefabsAdjusted", prefabReplacementMap.Count);
                }
            }
            
            EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Success"), resultMessage, FaceAnimOptimizerLocalization.L("OK"));
            
EditorUtility.FocusProjectWindow();
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputFolder);
            if (folderAsset != null)
            {
                Selection.activeObject = folderAsset;
            }
        }
        
        private void CreateMainAdjustedCurves(AnimationClip sourceClip, AnimationClip newClip)
        {
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(sourceClip);
            
            foreach (EditorCurveBinding binding in curveBindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    
                    BlendShapeInfo info;
                    if (mainBlendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected)
                    {
                        AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                        AnimationCurve adjustedCurve = CreateAdjustedCurve(sourceCurve, info.value);
                        AnimationUtility.SetEditorCurve(newClip, binding, adjustedCurve);
                    }
                    else
                    {
                        AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                        AnimationUtility.SetEditorCurve(newClip, binding, sourceCurve);
                    }
                }
                else
                {
                    AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                    AnimationUtility.SetEditorCurve(newClip, binding, sourceCurve);
                }
            }
        }

        private AnimationCurve CreateAdjustedCurve(AnimationCurve sourceCurve, float minimumValue)
        {
            AnimationCurve adjustedCurve = new AnimationCurve();
            
            foreach (Keyframe key in sourceCurve.keys)
            {
                // 元の値と最低保証値の最大値を取る（従来の動作）
                float newValue = Mathf.Max(key.value, minimumValue);
                
                Keyframe newKey = new Keyframe(
                    key.time, 
                    newValue, 
                    key.inTangent, 
                    key.outTangent, 
                    key.inWeight, 
                    key.outWeight
                );
                adjustedCurve.AddKey(newKey);
            }
            
            return adjustedCurve;
        }
        
        // 共通のメソッド（Controller・Prefab操作）
        private GameObject CreatePrefabVariant(GameObject sourcePrefab, string outputFolder, Dictionary<AnimatorController, AnimatorController> controllerMap)
        {
            if (sourcePrefab == null)
                return null;
                
            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            string fileName = Path.GetFileName(sourcePath);
            string destinationPath = $"{outputFolder}/FAO_{fileName}";
            
            if (File.Exists(destinationPath))
            {
                int counter = 1;
                while (File.Exists($"{outputFolder}/FAO_{counter}_{fileName}"))
                {
                    counter++;
                }
                destinationPath = $"{outputFolder}/FAO_{counter}_{fileName}";
            }
            
            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            
            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator.runtimeAnimatorController == null)
                    continue;
                
                RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
                string controllerPath = AssetDatabase.GetAssetPath(runtimeController);
                
                foreach (var pair in controllerMap)
                {
                    string origPath = AssetDatabase.GetAssetPath(pair.Key);
                    
                    if (controllerPath == origPath)
                    {
                        animator.runtimeAnimatorController = pair.Value;
                        break;
                    }
                }
            }
            
            var maComponents = instance.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && c.GetType().Name.Contains("MergeAnimator"))
                .ToArray();
                
            foreach (var maComponent in maComponents)
            {
                SerializedObject serializedMA = new SerializedObject(maComponent);
                SerializedProperty animatorProp = serializedMA.FindProperty("animator");
                
                if (animatorProp != null && animatorProp.propertyType == SerializedPropertyType.ObjectReference && 
                    animatorProp.objectReferenceValue != null)
                {
                    RuntimeAnimatorController controller = animatorProp.objectReferenceValue as RuntimeAnimatorController;
                    if (controller != null)
                    {
                        string controllerPath = AssetDatabase.GetAssetPath(controller);
                        
                        foreach (var pair in controllerMap)
                        {
                            string origPath = AssetDatabase.GetAssetPath(pair.Key);
                            
                            if (controllerPath == origPath)
                            {
                                animatorProp.objectReferenceValue = pair.Value;
                                serializedMA.ApplyModifiedProperties();
                                break;
                            }
                        }
                    }
                }
            }
            
            PrefabUtility.SaveAsPrefabAsset(instance, destinationPath);
            UnityEngine.Object.DestroyImmediate(instance);
            
            return AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
        }
        
        private void ReplacePrefabControllers(GameObject prefab, 
                                        Dictionary<AnimatorController, AnimatorController> controllerMap,
                                        Dictionary<AnimationClip, AnimationClip> animMap)
        {
            if (prefab == null || controllerMap.Count == 0)
                return;
            
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool prefabModified = false;
            
            try
            {
                Animator[] animators = prefabRoot.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators)
                {
                    if (animator.runtimeAnimatorController == null)
                        continue;
                    
                    RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
                    string controllerPath = AssetDatabase.GetAssetPath(runtimeController);
                    
                    foreach (var pair in controllerMap)
                    {
                        string origPath = AssetDatabase.GetAssetPath(pair.Key);
                        
                        if (controllerPath == origPath)
                        {
                            animator.runtimeAnimatorController = pair.Value;
                            prefabModified = true;
                            break;
                        }
                    }
                }
                
                try
                {
                    var mergeAnimatorComponents = prefabRoot.GetComponentsInChildren<Component>(true)
                        .Where(c => c != null && c.GetType().Name.Contains("MergeAnimator"))
                        .ToArray();
                    
                    foreach (var maComponent in mergeAnimatorComponents)
                    {
                        SerializedObject serializedMA = new SerializedObject(maComponent);
                        SerializedProperty animatorProp = serializedMA.FindProperty("animator");
                        
                        if (animatorProp != null && animatorProp.propertyType == SerializedPropertyType.ObjectReference && 
                            animatorProp.objectReferenceValue != null)
                        {
                            RuntimeAnimatorController controller = animatorProp.objectReferenceValue as RuntimeAnimatorController;
                            if (controller != null)
                            {
                                string controllerPath = AssetDatabase.GetAssetPath(controller);
                                
                                foreach (var pair in controllerMap)
                                {
                                    string origPath = AssetDatabase.GetAssetPath(pair.Key);
                                    
                                    if (controllerPath == origPath)
                                    {
                                        animatorProp.objectReferenceValue = pair.Value;
                                        serializedMA.ApplyModifiedProperties();
                                        prefabModified = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error with MergeAnimator: {ex.Message}");
                }
                
                if (prefabModified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        
        private bool ReplaceAnimationsInController(AnimatorController controller, Dictionary<AnimationClip, AnimationClip> replacementMap)
        {
            if (controller == null)
                return false;
                
            int replacedCount = 0;
            bool controllerModified = false;
            
            try
            {
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    if (layer.stateMachine != null)
                    {
                        int layerReplacements = ReplaceAnimationsInStateMachine(layer.stateMachine, replacementMap);
                        replacedCount += layerReplacements;
                        
                        if (layerReplacements > 0)
                            controllerModified = true;
                    }
                }
                
                if (controllerModified)
                {
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error replacing animations: {ex.Message}");
                return false;
            }
            
            return controllerModified;
        }
        
        private int ReplaceAnimationsInStateMachine(AnimatorStateMachine stateMachine, Dictionary<AnimationClip, AnimationClip> replacementMap)
        {
            int replacedCount = 0;
            
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                AnimatorState state = childState.state;
                
                if (state.motion is AnimationClip)
                {
                    AnimationClip clip = state.motion as AnimationClip;
                    if (replacementMap.TryGetValue(clip, out AnimationClip newClip))
                    {
                        state.motion = newClip;
                        EditorUtility.SetDirty(state);
                        replacedCount++;
                    }
                }
                else if (state.motion is BlendTree)
                {
                    replacedCount += ReplaceAnimationsInBlendTree(state.motion as BlendTree, replacementMap);
                }
            }
            
            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                replacedCount += ReplaceAnimationsInStateMachine(childStateMachine.stateMachine, replacementMap);
            }
            
            return replacedCount;
        }
        
        private int ReplaceAnimationsInBlendTree(BlendTree blendTree, Dictionary<AnimationClip, AnimationClip> replacementMap)
        {
            int replacedCount = 0;
            
            var children = blendTree.children;
            bool hasChanges = false;
            
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                
                if (child.motion is AnimationClip)
                {
                    AnimationClip clip = child.motion as AnimationClip;
                    if (replacementMap.TryGetValue(clip, out AnimationClip newClip))
                    {
                        child.motion = newClip;
                        hasChanges = true;
                        replacedCount++;
                    }
                }
                else if (child.motion is BlendTree)
                {
                    replacedCount += ReplaceAnimationsInBlendTree(child.motion as BlendTree, replacementMap);
                }
                
                children[i] = child;
            }
            
            if (hasChanges)
            {
                blendTree.children = children;
                EditorUtility.SetDirty(blendTree);
            }
            
            return replacedCount;
        }
    }
}
