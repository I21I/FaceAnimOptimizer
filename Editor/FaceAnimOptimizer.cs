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
        private List<AnimationClip> sourceAnimations = new List<AnimationClip>();
        private bool[] animationSelected;
        private Vector2 scrollPosition;
        private Vector2 animScrollPosition;
        private Vector2 controllerScrollPosition;
        private Vector2 prefabScrollPosition;
        private Vector2 blendShapeScrollPosition;
        
        private string baseOutputPath = "Assets/21CSXtools/FaceAnimOptimizer";
        private string outputFolderName = "";
        private string outputAnimationPrefix = "";
        
        private List<string> targetBlendShapes = new List<string>();
        private Dictionary<string, BlendShapeInfo> blendShapeInfos = new Dictionary<string, BlendShapeInfo>();
        private bool hasShownBlendShapeInfo = false;
        
        private Language currentLanguage = Language.Japanese;
        
        private bool autoReplaceSettingsFoldout = true;
        
        private GUIStyle dropAreaStyle;
        private GUIStyle dropAreaTextStyle;
        private GUIStyle thinSeparatorStyle;
        
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
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
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
            
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(FaceAnimOptimizerLocalization.L("TargetMesh"), EditorStyles.boldLabel, GUILayout.Width(100));
            SkinnedMeshRenderer previousMeshRenderer = targetMeshRenderer;
            targetMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                targetMeshRenderer, typeof(SkinnedMeshRenderer), true, GUILayout.ExpandWidth(true));
                
            if (previousMeshRenderer != targetMeshRenderer)
            {
                hasShownBlendShapeInfo = false;
                blendShapeInfos.Clear();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
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
            
            DrawAnimationSection();
            
            EditorGUILayout.Space(3);
            
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GetCurrentValues"), GUILayout.Height(30)))
            {
                CaptureCurrentBlendShapeValues();
                hasShownBlendShapeInfo = true;
            }
            
            EditorGUILayout.Space(3);
            DrawThinSeparator();
            EditorGUILayout.Space(3);
            
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
            
            EditorGUILayout.Space(3);
            
            DrawOutputSettings();
            
            EditorGUILayout.Space(3);
            
            GUI.enabled = CanGenerateAnimations() && hasShownBlendShapeInfo;
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("GenerateAnimations"), GUILayout.Height(30)))
            {
                GenerateAdjustedAnimations();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
        
        private void DrawAnimationSection()
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
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("SelectAll")))
            {
                foreach (var name in targetBlendShapes)
                {
                    var info = blendShapeInfos[name];
                    info.selected = true;
                    blendShapeInfos[name] = info;
                }
            }
            if (GUILayout.Button(FaceAnimOptimizerLocalization.L("UnselectAll")))
            {
                foreach (var name in targetBlendShapes)
                {
                    var info = blendShapeInfos[name];
                    info.selected = false;
                    blendShapeInfos[name] = info;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            blendShapeScrollPosition = EditorGUILayout.BeginScrollView(
                blendShapeScrollPosition, GUILayout.Height(150), GUILayout.ExpandWidth(true));
                
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2 - 15));
            int halfCount = targetBlendShapes.Count / 2 + targetBlendShapes.Count % 2;
            for (int i = 0; i < halfCount; i++)
            {
                string name = targetBlendShapes[i];
                var info = blendShapeInfos[name];
                
                EditorGUILayout.BeginHorizontal();
                
                bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                if (newSelected != info.selected)
                {
                    info.selected = newSelected;
                    blendShapeInfos[name] = info;
                }
                
                EditorGUILayout.LabelField(name, GUILayout.Width(130));
                EditorGUILayout.LabelField($"{info.value:F1}%", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2 - 15));
            for (int i = halfCount; i < targetBlendShapes.Count; i++)
            {
                string name = targetBlendShapes[i];
                var info = blendShapeInfos[name];
                
                EditorGUILayout.BeginHorizontal();
                
                bool newSelected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(20));
                if (newSelected != info.selected)
                {
                    info.selected = newSelected;
                    blendShapeInfos[name] = info;
                }
                
                EditorGUILayout.LabelField(name, GUILayout.Width(130));
                EditorGUILayout.LabelField($"{info.value:F1}%", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
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
                AssetDatabase.CreateFolder(baseOutputPath, outputFolderName);
            }
            
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
                        
                        AnimationCurve adjustedCurve = new AnimationCurve();
                        
                        foreach (Keyframe key in sourceCurve.keys)
                        {
                            float newValue = Mathf.Max(key.value, info.value);
                            
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
