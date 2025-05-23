using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceAnimOptimizer
{
    public class FaceAnimOptimizer : EditorWindow
    {
        private SkinnedMeshRenderer targetMeshRenderer;
        
        // タブ管理
        private int currentTab = 0;
        private string[] tabNames = { "通常オプティマイズ", "片目アニメーション" };
        
        // 調節対象のアニメーション（既存機能）
        private List<AnimationClip> sourceAnimations = new List<AnimationClip>();
        private bool[] animationSelected;
        
        // 表情アニメーション参照（BlendShape抽出用）
        private List<AnimationClip> referenceAnimations = new List<AnimationClip>();
        
        private Vector2 scrollPosition;
        private Vector2 animScrollPosition;
        private Vector2 referenceScrollPosition;
        private Vector2 controllerScrollPosition;
        private Vector2 prefabScrollPosition;
        private Vector2 blendShapeScrollPosition;
        private Vector2 manualSelectionScrollPosition;
        
        private string baseOutputPath = "Assets/21CSXtools/FaceAnimOptimizer";
        private string outputFolderName = "";
        private string outputAnimationPrefix = "";
        
        private List<string> targetBlendShapes = new List<string>();
        private Dictionary<string, BlendShapeInfo> blendShapeInfos = new Dictionary<string, BlendShapeInfo>();
        private bool hasShownBlendShapeInfo = false;
        
        private Language currentLanguage = Language.Japanese;
        
        private bool autoReplaceSettingsFoldout = true;
        private bool manualSelectionFoldout = false;
        
        private GUIStyle dropAreaStyle;
        private GUIStyle dropAreaTextStyle;
        private GUIStyle thinSeparatorStyle;
        
        // 手動選択用
        private List<string> availableBlendShapes = new List<string>();
        private Dictionary<string, bool> manualSelectionStates = new Dictionary<string, bool>();
        private bool showLRPatternOnly = false;
        
        // 片目アニメーション生成用
        private AnimationClip baseAnimation;
        
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
                hasShownBlendShapeInfo = false;
                blendShapeInfos.Clear();
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
            
            // 表情アニメーション参照エリア
            DrawReferenceAnimationSection();
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // 調節対象アニメーション
            DrawSourceAnimationSection();
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
            // BlendShape取得・設定
            DrawBlendShapeSection();
            
            EditorGUILayout.Space(3);
            
            // 出力設定
            DrawOutputSettings();
            
            EditorGUILayout.Space(3);
            
            // メイン機能ボタン
            GUI.enabled = CanGenerateAnimations() && hasShownBlendShapeInfo;
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateAnimations"), GUILayout.Height(30)))
            {
                GenerateAdjustedAnimations();
            }
            GUI.enabled = true;
        }
        
        private void DrawEyeAnimationTab()
        {
            EditorGUILayout.LabelField("片目アニメーション生成", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // ベースアニメーション選択
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("BaseAnimation"), EditorStyles.boldLabel);
            baseAnimation = (AnimationClip)EditorGUILayout.ObjectField(
                baseAnimation, typeof(AnimationClip), false, GUILayout.ExpandWidth(true));
            
            EditorGUILayout.Space(3);
            
            // BlendShape設定が必要な旨を表示
            if (!hasShownBlendShapeInfo || blendShapeInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("先に「通常オプティマイズ」タブでBlendShapeを設定してください。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"設定済みBlendShape: {blendShapeInfos.Count}個", EditorStyles.miniLabel);
                
                // L/Rパターンの確認表示
                var leftShapes = targetBlendShapes.Where(FaceAnimOptimizerUtils.IsLeftEyeBlendShape).ToList();
                var rightShapes = targetBlendShapes.Where(FaceAnimOptimizerUtils.IsRightEyeBlendShape).ToList();
                
                EditorGUILayout.LabelField($"左目用: {leftShapes.Count}個, 右目用: {rightShapes.Count}個", EditorStyles.miniLabel);
                
                // 詳細表示
                if (leftShapes.Count > 0 || rightShapes.Count > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (leftShapes.Count > 0)
                    {
                        EditorGUILayout.LabelField("左目用BlendShape:", EditorStyles.boldLabel);
                        foreach (var shape in leftShapes)
                        {
                            EditorGUILayout.LabelField($"  • {shape}");
                        }
                    }
                    if (rightShapes.Count > 0)
                    {
                        EditorGUILayout.LabelField("右目用BlendShape:", EditorStyles.boldLabel);
                        foreach (var shape in rightShapes)
                        {
                            EditorGUILayout.LabelField($"  • {shape}");
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.Space(5);
            
            // 生成ボタン
            GUI.enabled = baseAnimation != null && hasShownBlendShapeInfo && blendShapeInfos.Count > 0;
            
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
            EditorGUILayout.LabelField("1. 通常オプティマイズタブでBlendShapeを設定");
            EditorGUILayout.LabelField("2. ベースとなるまばたきアニメーションを選択");
            EditorGUILayout.LabelField("3. 左目版・右目版・両目版のボタンを押す");
            EditorGUILayout.LabelField("4. 設定されたBlendShape値が最低値として保証される");
            EditorGUILayout.EndVertical();
        }
        
        private void DrawReferenceAnimationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("ReferenceAnimations"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("ReferenceAnimationsDesc"), EditorStyles.miniLabel);
            
            // 自動取得ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("アバターから自動取得"))
            {
                AutoDetectExpressionAnimations();
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("Clear")))
            {
                referenceAnimations.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            // ドラッグ&ドロップエリア
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 30.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "", dropAreaStyle);
            EditorGUI.LabelField(dropArea, "表情アニメーションをここにドラッグ＆ドロップ", dropAreaTextStyle);
            HandleReferenceAnimationDragAndDrop(dropArea);
            
            // 抽出ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("ExtractBlendShapes")))
            {
                ExtractBlendShapesFromReferenceAnimations();
            }
            EditorGUILayout.EndHorizontal();
            
            // 参照アニメーション一覧
            if (referenceAnimations.Count > 0)
            {
                referenceScrollPosition = EditorGUILayout.BeginScrollView(
                    referenceScrollPosition, GUILayout.Height(80), GUILayout.ExpandWidth(true));
                
                for (int i = 0; i < referenceAnimations.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(referenceAnimations[i], typeof(AnimationClip), false);
                    
                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        referenceAnimations.RemoveAt(i);
                        GUIUtility.ExitGUI();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void AutoDetectExpressionAnimations()
        {
            if (targetMeshRenderer == null)
            {
                EditorUtility.DisplayDialog("エラー", "対象メッシュが設定されていません。", "OK");
                return;
            }
            
            // アバターオブジェクトを特定
            GameObject avatarRoot = GetAvatarRoot(targetMeshRenderer);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog("エラー", "アバターのルートオブジェクトが見つかりません。", "OK");
                return;
            }
            
            List<AnimationClip> foundAnimations = new List<AnimationClip>();
            
            // 1. AnimatorControllerから表情アニメーションを検索
            Animator animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = FindGestureAnimations(animator.runtimeAnimatorController as AnimatorController);
                foundAnimations.AddRange(clips);
            }
            
            // 2. Facialフォルダから検索
            var facialAnimations = FindFacialAnimations();
            foundAnimations.AddRange(facialAnimations);
            
            // 重複を除去して追加
            int addedCount = 0;
            foreach (var clip in foundAnimations)
            {
                if (clip != null && !referenceAnimations.Contains(clip))
                {
                    referenceAnimations.Add(clip);
                    addedCount++;
                }
            }
            
            if (addedCount > 0)
            {
                EditorUtility.DisplayDialog("完了", $"{addedCount}個の表情アニメーションを検出しました。", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("お知らせ", "表情アニメーションが見つかりませんでした。", "OK");
            }
        }
        
        private GameObject GetAvatarRoot(SkinnedMeshRenderer renderer)
        {
            Transform current = renderer.transform;
            
            // 上に向かってAvatarDescriptorを探す
            while (current != null)
            {
                if (current.GetComponent<Component>()?.GetType().Name.Contains("VRCAvatarDescriptor") == true)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            
            // 見つからない場合は、ルートオブジェクトを返す
            current = renderer.transform;
            while (current.parent != null)
            {
                current = current.parent;
            }
            
            return current.gameObject;
        }
        
        private List<AnimationClip> FindGestureAnimations(AnimatorController controller)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            
            if (controller == null) return clips;
            
            // FXレイヤーを探す
            foreach (var layer in controller.layers)
            {
                if (layer.name.ToLower().Contains("fx") || 
                    layer.name.ToLower().Contains("gesture") || 
                    layer.name.ToLower().Contains("hand"))
                {
                    CollectGestureClips(layer.stateMachine, clips);
                }
            }
            
            return clips;
        }
        
        private void CollectGestureClips(AnimatorStateMachine stateMachine, List<AnimationClip> clips)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.motion is AnimationClip clip)
                {
                    // 表情関連のアニメーションを判定
                    if (FaceAnimOptimizerUtils.IsExpressionAnimation(clip))
                    {
                        clips.Add(clip);
                    }
                }
                else if (state.state.motion is BlendTree blendTree)
                {
                    CollectGestureClipsFromBlendTree(blendTree, clips);
                }
            }
            
            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                CollectGestureClips(subStateMachine.stateMachine, clips);
            }
        }
        
        private void CollectGestureClipsFromBlendTree(BlendTree blendTree, List<AnimationClip> clips)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    if (FaceAnimOptimizerUtils.IsExpressionAnimation(clip))
                    {
                        clips.Add(clip);
                    }
                }
                else if (child.motion is BlendTree subTree)
                {
                    CollectGestureClipsFromBlendTree(subTree, clips);
                }
            }
        }
        
        private List<AnimationClip> FindFacialAnimations()
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            
            // プロジェクト内のすべてのアニメーションクリップを検索
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Facialフォルダに含まれるかチェック
                if (path.ToLower().Contains("facial"))
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (FaceAnimOptimizerUtils.IsExpressionAnimation(clip))
                    {
                        clips.Add(clip);
                    }
                }
            }
            
            return clips;
        }
        
        private void DrawSourceAnimationSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(FaceAnimOptimizerLocalization.L("AddAnimation"), EditorStyles.boldLabel);
            
            GUILayout.Space(20);
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("AddFromControllers"), GUILayout.Width(180)))
            {
                AddAnimationsFromControllers();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 40.0f, GUILayout.Width(Screen.width - 15), GUILayout.MaxWidth(Screen.width - 15));
            dropArea.x += 15;
            GUI.Box(dropArea, "", dropAreaStyle);
            
            EditorGUI.LabelField(dropArea, FaceAnimOptimizerLocalization.L("DragDropAnimation"), dropAreaTextStyle);
            
            HandleDragAndDrop(dropArea);
            
            if (sourceAnimations.Count > 0)
            {
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button(FaceAnimOptimizerLocalization.L("DeleteAll")))
                {
                    sourceAnimations.Clear();
                    animationSelected = null;
                }
            }
            
            DisplayAnimationsList();
        }
        
        private void DrawBlendShapeSection()
        {
            // BlendShape取得ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GetCurrentValues"), GUILayout.Height(30)))
            {
                CaptureCurrentBlendShapeValues();
                hasShownBlendShapeInfo = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            // 手動選択UI
            DrawManualSelectionUI();
            
            // BlendShape値表示
            if (targetMeshRenderer != null && hasShownBlendShapeInfo)
            {
                if (blendShapeInfos.Count > 0)
                {
                    DisplayBlendShapeValues();
                }
                else
                {
                    EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                }
            }
        }
        
        private void DrawManualSelectionUI()
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
                    AddSelectedBlendShapes();
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
        
        private void AddSelectedBlendShapes()
        {
            blendShapeInfos.Clear();
            targetBlendShapes.Clear();
            
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
                        targetBlendShapes.Add(shapeName);
                        blendShapeInfos[shapeName] = new BlendShapeInfo { value = value, selected = true };
                        foundSelected = true;
                    }
                }
            }
            
            if (foundSelected)
            {
                hasShownBlendShapeInfo = true;
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Complete"),
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", targetBlendShapes.Count),
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
        
        private void HandleReferenceAnimationDragAndDrop(Rect dropArea)
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
                            if (!referenceAnimations.Contains(clip))
                            {
                                referenceAnimations.Add(clip);
                            }
                        }
                    }
                    
                    Repaint();
                    currentEvent.Use();
                    break;
            }
        }
        
        private void ExtractBlendShapesFromReferenceAnimations()
        {
            if (referenceAnimations.Count == 0)
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
            
            // 参照アニメーションからBlendShapeを抽出
            HashSet<string> extractedBlendShapes = FaceAnimOptimizerUtils.ExtractBlendShapesFromAnimations(referenceAnimations);
            
            if (extractedBlendShapes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Notice"),
                    FaceAnimOptimizerLocalization.L("NoBlendShapesInAnimations"),
                    FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            blendShapeInfos.Clear();
            targetBlendShapes.Clear();
            
            bool foundActiveBlendShapes = false;
            
            foreach (string shapeName in extractedBlendShapes)
            {
                int shapeIndex = targetMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName);
                if (shapeIndex >= 0)
                {
                    float value = targetMeshRenderer.GetBlendShapeWeight(shapeIndex);
                    
                    if (value > 0.001f)
                    {
                        targetBlendShapes.Add(shapeName);
                        blendShapeInfos[shapeName] = new BlendShapeInfo { value = value, selected = true };
                        foundActiveBlendShapes = true;
                    }
                }
            }
            
            if (foundActiveBlendShapes)
            {
                hasShownBlendShapeInfo = true;
                EditorUtility.DisplayDialog(
                    FaceAnimOptimizerLocalization.L("Complete"),
                    FaceAnimOptimizerLocalization.L("BlendShapesExtracted", targetBlendShapes.Count),
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
            
            string newClipName = baseAnimation.name + suffix;
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
                    
                    // 対象のBlendShapeかどうかを判定
                    switch (eyeType)
                    {
                        case EyeType.LeftOnly:
                            shouldApplyBlendShape = FaceAnimOptimizerUtils.IsLeftEyeBlendShape(blendShapeName) &&
                                                  blendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
                            break;
                        case EyeType.RightOnly:
                            shouldApplyBlendShape = FaceAnimOptimizerUtils.IsRightEyeBlendShape(blendShapeName) &&
                                                  blendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
                            break;
                        case EyeType.Both:
                            shouldApplyBlendShape = (FaceAnimOptimizerUtils.IsLeftEyeBlendShape(blendShapeName) || 
                                                   FaceAnimOptimizerUtils.IsRightEyeBlendShape(blendShapeName)) &&
                                                  blendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected;
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
        
        private void DrawOutputSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputSettings"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputFolder"), GUILayout.Width(100));
            EditorGUILayout.LabelField(baseOutputPath + "/Anim[日付]");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("OutputPrefix"), GUILayout.Width(100));
            outputAnimationPrefix = EditorGUILayout.TextField(outputAnimationPrefix);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleDragAndDrop(Rect dropArea)
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
                            AddAnimationClip(draggedObject as AnimationClip);
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
        
        private void AddAnimationClip(AnimationClip clip)
        {
            if (clip != null && !sourceAnimations.Contains(clip))
            {
                sourceAnimations.Add(clip);
                
                sourceAnimations.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                
                int newIndex = sourceAnimations.IndexOf(clip);
                
                bool[] newArray = new bool[sourceAnimations.Count];
                
                if (animationSelected != null)
                {
                    for (int i = 0; i < animationSelected.Length; i++)
                    {
                        if (i < sourceAnimations.Count)
                        {
                            newArray[i] = animationSelected[i];
                        }
                    }
                }
                
                for (int i = 0; i < newArray.Length; i++)
                {
                    newArray[i] = true;
                }
                
                animationSelected = newArray;
            }
        }
        
        private void DisplayAnimationsList()
        {
            if (sourceAnimations.Count == 0)
            {
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                for (int i = 0; i < animationSelected.Length; i++)
                    animationSelected[i] = true;
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                for (int i = 0; i < animationSelected.Length; i++)
                    animationSelected[i] = false;
            }
            EditorGUILayout.EndHorizontal();
            
            float listHeight = Mathf.Min(120, sourceAnimations.Count * 20 + 10);
            animScrollPosition = EditorGUILayout.BeginScrollView(animScrollPosition, GUILayout.Height(listHeight));
            
            for (int i = 0; i < sourceAnimations.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (animationSelected != null && i < animationSelected.Length)
                {
                    animationSelected[i] = EditorGUILayout.Toggle(animationSelected[i], GUILayout.Width(20));
                }
                
                EditorGUILayout.ObjectField(sourceAnimations[i], typeof(AnimationClip), false);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    sourceAnimations.RemoveAt(i);
                    
                    if (sourceAnimations.Count > 0 && animationSelected != null)
                    {
                        bool[] newArray = new bool[sourceAnimations.Count];
                        for (int j = 0; j < i && j < animationSelected.Length; j++)
                            newArray[j] = animationSelected[j];
                        for (int j = i; j < sourceAnimations.Count && j+1 < animationSelected.Length; j++)
                            newArray[j] = animationSelected[j + 1];
                        animationSelected = newArray;
                    }
                    else
                    {
                        animationSelected = null;
                    }
                    
                    GUIUtility.ExitGUI();
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void AddAnimationsFromControllers()
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
                    if (clip != null && !sourceAnimations.Contains(clip) && uniqueClips.Add(clip))
                    {
                        sourceAnimations.Add(clip);
                        addedCount++;
                    }
                }
            }
            
            sourceAnimations.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            
            bool[] newArray = new bool[sourceAnimations.Count];
            if (animationSelected != null)
            {
                for (int i = 0; i < Mathf.Min(animationSelected.Length, sourceAnimations.Count); i++)
                {
                    newArray[i] = animationSelected[i];
                }
            }
            
            for (int i = 0; i < sourceAnimations.Count; i++)
            {
                if (i >= animationSelected?.Length || animationSelected == null)
                {
                    newArray[i] = true;
                }
            }
            
            animationSelected = newArray;
            
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
        
        private void CaptureCurrentBlendShapeValues()
        {
            if (targetMeshRenderer == null || targetMeshRenderer.sharedMesh == null)
            {
                EditorUtility.DisplayDialog(FaceAnimOptimizerLocalization.L("Error"), FaceAnimOptimizerLocalization.L("NoMeshSelected"), FaceAnimOptimizerLocalization.L("OK"));
                return;
            }
            
            blendShapeInfos.Clear();
            targetBlendShapes.Clear();
            
            Mesh mesh = targetMeshRenderer.sharedMesh;
            bool foundActiveBlendShapes = false;
            
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                float value = targetMeshRenderer.GetBlendShapeWeight(i);
                
                if (value > 0.001f)
                {
                    targetBlendShapes.Add(name);
                    blendShapeInfos[name] = new BlendShapeInfo { value = value, selected = true };
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
                    FaceAnimOptimizerLocalization.L("BlendShapeCaptured", targetBlendShapes.Count), 
                    FaceAnimOptimizerLocalization.L("OK"));
            }
        }
        
        private void DisplayBlendShapeValues()
        {
            if (targetBlendShapes.Count == 0)
            {
                EditorGUILayout.HelpBox(FaceAnimOptimizerLocalization.L("NoBlendShapesMessage"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("CapturedBlendShapes"), EditorStyles.boldLabel);
            
            // デバッグ情報表示
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"総数: {targetBlendShapes.Count}個", EditorStyles.miniLabel);
            if (GUILayout.Button("デバッグ出力", GUILayout.Width(80)))
            {
                FaceAnimOptimizerUtils.DebugBlendShapeNames(targetBlendShapes, "DisplayBlendShapeValues");
                
                // BlendShapeInfoの内容も確認
                Debug.Log("=== BlendShapeInfo辞書の内容 ===");
                foreach (var kvp in blendShapeInfos)
                {
                    Debug.Log($"Key: '{kvp.Key}' Value: {kvp.Value.value:F1}% Selected: {kvp.Value.selected}");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                foreach (var name in targetBlendShapes)
                {
                    if (blendShapeInfos.ContainsKey(name))
                    {
                        var info = blendShapeInfos[name];
                        info.selected = true;
                        blendShapeInfos[name] = info;
                    }
                }
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                foreach (var name in targetBlendShapes)
                {
                    if (blendShapeInfos.ContainsKey(name))
                    {
                        var info = blendShapeInfos[name];
                        info.selected = false;
                        blendShapeInfos[name] = info;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            blendShapeScrollPosition = EditorGUILayout.BeginScrollView(
                blendShapeScrollPosition, GUILayout.Height(150), GUILayout.ExpandWidth(true));
                
            EditorGUILayout.BeginHorizontal();
            
            // 左列
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2 - 15));
            int halfCount = targetBlendShapes.Count / 2 + targetBlendShapes.Count % 2;
            for (int i = 0; i < halfCount; i++)
            {
                string name = targetBlendShapes[i];
                if (blendShapeInfos.ContainsKey(name))
                {
                    var info = blendShapeInfos[name];
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                    if (newSelected != info.selected)
                    {
                        info.selected = newSelected;
                        blendShapeInfos[name] = info;
                    }
                    
                    // BlendShape名を完全表示（省略しない）
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
            EditorGUILayout.EndVertical();
            
            // 右列
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2 - 15));
            for (int i = halfCount; i < targetBlendShapes.Count; i++)
            {
                string name = targetBlendShapes[i];
                if (blendShapeInfos.ContainsKey(name))
                {
                    var info = blendShapeInfos[name];
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                    if (newSelected != info.selected)
                    {
                        info.selected = newSelected;
                        blendShapeInfos[name] = info;
                    }
                    
                    // BlendShape名を完全表示（省略しない）
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
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndScrollView();
        }
        
        private bool CanGenerateAnimations()
        {
            return targetMeshRenderer != null && 
                sourceAnimations.Count > 0 && 
                animationSelected != null;
        }
        
        private void GenerateAdjustedAnimations()
        {
            outputFolderName = "Anim" + DateTime.Now.ToString("yyMMddHHmmss");
            string outputFolder = baseOutputPath + "/" + outputFolderName;
            
            CreateOutputFolder(outputFolder);
            
            Dictionary<AnimationClip, AnimationClip> animReplacementMap = new Dictionary<AnimationClip, AnimationClip>();
            Dictionary<AnimatorController, AnimatorController> controllerReplacementMap = new Dictionary<AnimatorController, AnimatorController>();
            Dictionary<GameObject, GameObject> prefabReplacementMap = new Dictionary<GameObject, GameObject>();
            int processedCount = 0;
            
            for (int i = 0; i < sourceAnimations.Count; i++)
            {
                if (animationSelected == null || i >= animationSelected.Length || !animationSelected[i])
                    continue;
                    
                AnimationClip sourceClip = sourceAnimations[i];
                if (sourceClip == null)
                    continue;
                    
                string newClipName;
                if (string.IsNullOrEmpty(outputAnimationPrefix))
                {
                    newClipName = "FAO_" + sourceClip.name;
                }
                else
                {
                    newClipName = outputAnimationPrefix + sourceClip.name;
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
                
                CreateAdjustedCurves(sourceClip, newClip);
                
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
        
        private void CreateAdjustedCurves(AnimationClip sourceClip, AnimationClip newClip)
        {
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(sourceClip);
            
            foreach (EditorCurveBinding binding in curveBindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    
                    BlendShapeInfo info;
                    if (blendShapeInfos.TryGetValue(blendShapeName, out info) && info.selected)
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
