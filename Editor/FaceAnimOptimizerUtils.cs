using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceAnimOptimizer
{
    public static class FaceAnimOptimizerUtils
    {
        public static Texture2D CreateSimpleBorderTexture()
        {
            int size = 12;
            Texture2D texture = new Texture2D(size, size);
            
            Color fillColor = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, fillColor);
                }
            }
            
            Color borderColor = EditorGUIUtility.isProSkin 
                ? new Color(0.7f, 0.7f, 0.7f, 0.6f)
                : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            
            for (int i = 0; i < size; i++)
            {
                texture.SetPixel(i, size - 1, borderColor);
                texture.SetPixel(i, 0, borderColor);
                texture.SetPixel(0, i, borderColor);
                texture.SetPixel(size - 1, i, borderColor);
            }
            
            texture.Apply();
            return texture;
        }
        
        public static string GetUniqueGroupName(Transform parent, string baseName)
        {
            var existingObjects = parent.Cast<Transform>()
                .Select(t => t.name)
                .Where(name => name.StartsWith(baseName))
                .ToList();

            if (!existingObjects.Contains(baseName))
                return baseName;

            int maxNumber = existingObjects
                .Select(name =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"\((\d+)\)$");
                    return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                })
                .Max();

            return $"{baseName} ({maxNumber + 1})";
        }
        
        public static AnimatorController CopyAnimatorController(AnimatorController source, string outputFolder)
        {
            if (source == null)
                return null;
                
            string sourcePath = AssetDatabase.GetAssetPath(source);
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
            
            try
            {
                AssetDatabase.CopyAsset(sourcePath, destinationPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(destinationPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{FaceAnimOptimizerLocalization.L("Error")} {FaceAnimOptimizerLocalization.L("CopyingController")}: {sourcePath} - {ex.Message}");
                return null;
            }
        }
        
        public static GameObject CopyPrefab(GameObject sourcePrefab, string outputFolder)
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
            
            AssetDatabase.CopyAsset(sourcePath, destinationPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            return AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
        }
        
        public static void CollectAnimationClipsFromController(AnimatorController controller, List<AnimationClip> clips)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine != null)
                {
                    CollectAnimationClipsFromStateMachine(layer.stateMachine, clips);
                }
            }
        }
        
        public static void CollectAnimationClipsFromStateMachine(AnimatorStateMachine stateMachine, List<AnimationClip> clips)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                AnimatorState state = childState.state;
                
                if (state.motion is AnimationClip)
                {
                    AnimationClip clip = state.motion as AnimationClip;
                    if (clip != null && !clips.Contains(clip))
                    {
                        clips.Add(clip);
                    }
                }
                else if (state.motion is BlendTree)
                {
                    CollectAnimationClipsFromBlendTree(state.motion as BlendTree, clips);
                }
            }
            
            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                CollectAnimationClipsFromStateMachine(childStateMachine.stateMachine, clips);
            }
        }
        
        public static void CollectAnimationClipsFromBlendTree(BlendTree blendTree, List<AnimationClip> clips)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip)
                {
                    AnimationClip clip = child.motion as AnimationClip;
                    if (clip != null && !clips.Contains(clip))
                    {
                        clips.Add(clip);
                    }
                }
                else if (child.motion is BlendTree)
                {
                    CollectAnimationClipsFromBlendTree(child.motion as BlendTree, clips);
                }
            }
        }

        /// <summary>
        /// 改良された左目BlendShape判定（正確性重視・VRChat対応）
        /// </summary>
        public static bool IsLeftEyeBlendShape(string blendShapeName)
        {
            if (string.IsNullOrEmpty(blendShapeName))
                return false;
            
            // VRChatのリップシンク用は除外
            if (blendShapeName.ToLower().StartsWith("vrc."))
                return false;
            
            string lowerName = blendShapeName.ToLower();
            
            // 明確な左目パターンをチェック
            if (lowerName.Contains("left"))
                return true;
            
            // アンダーバー付きパターン（大文字小文字問わず）
            if (lowerName.Contains("_l") || lowerName.Contains("l_"))
                return true;
            
            // 末尾のL（大文字のみ - VRChatアバターの標準）
            if (blendShapeName.EndsWith("L"))
                return true;
            
            return false;
        }

        /// <summary>
        /// 改良された右目BlendShape判定（正確性重視・VRChat対応）
        /// </summary>
        public static bool IsRightEyeBlendShape(string blendShapeName)
        {
            if (string.IsNullOrEmpty(blendShapeName))
                return false;
            
            // VRChatのリップシンク用は除外
            if (blendShapeName.ToLower().StartsWith("vrc."))
                return false;
            
            string lowerName = blendShapeName.ToLower();
            
            // 明確な右目パターンをチェック
            if (lowerName.Contains("right"))
                return true;
            
            // アンダーバー付きパターン（大文字小文字問わず）
            if (lowerName.Contains("_r") || lowerName.Contains("r_"))
                return true;
            
            // 末尾のR（大文字のみ - VRChatアバターの標準）
            if (blendShapeName.EndsWith("R"))
                return true;
            
            return false;
        }

        /// <summary>
        /// アニメーションクリップからBlendShape名を抽出（完全版）
        /// </summary>
        public static HashSet<string> ExtractBlendShapesFromAnimation(AnimationClip clip)
        {
            HashSet<string> blendShapes = new HashSet<string>();
            
            if (clip == null) return blendShapes;
            
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            
            foreach (var binding in bindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && 
                    binding.propertyName.StartsWith("blendShape."))
                {
                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    
                    // 空文字やnullチェック
                    if (!string.IsNullOrEmpty(blendShapeName))
                    {
                        blendShapes.Add(blendShapeName);
                    }
                }
            }
            
            return blendShapes;
        }

        /// <summary>
        /// 複数のアニメーションクリップからBlendShape名を抽出
        /// </summary>
        public static HashSet<string> ExtractBlendShapesFromAnimations(List<AnimationClip> clips)
        {
            HashSet<string> allBlendShapes = new HashSet<string>();
            
            if (clips == null) return allBlendShapes;
            
            foreach (var clip in clips)
            {
                var blendShapes = ExtractBlendShapesFromAnimation(clip);
                foreach (var bs in blendShapes)
                {
                    allBlendShapes.Add(bs);
                }
            }
            
            return allBlendShapes;
        }

        /// <summary>
        /// L/RパターンのBlendShapeのみをフィルタリング（改良版）
        /// </summary>
        public static List<string> FilterBlendShapesByLRPattern(List<string> allBlendShapes)
        {
            List<string> filtered = new List<string>();
            
            if (allBlendShapes == null) return filtered;
            
            foreach (var shapeName in allBlendShapes)
            {
                if (IsLeftEyeBlendShape(shapeName) || IsRightEyeBlendShape(shapeName))
                {
                    filtered.Add(shapeName);
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// BlendShape名のデバッグ出力（開発用）
        /// </summary>
        public static void DebugBlendShapeNames(List<string> blendShapeNames, string context = "")
        {
            if (blendShapeNames == null || blendShapeNames.Count == 0)
            {
                Debug.Log($"[{context}] BlendShape名が空です");
                return;
            }
            
            Debug.Log($"[{context}] BlendShape一覧 ({blendShapeNames.Count}個):");
            for (int i = 0; i < blendShapeNames.Count; i++)
            {
                string name = blendShapeNames[i];
                bool isLeft = IsLeftEyeBlendShape(name);
                bool isRight = IsRightEyeBlendShape(name);
                string type = isLeft ? "[L]" : isRight ? "[R]" : "[?]";
                Debug.Log($"  {i:D2}: {name} {type}");
            }
        }

        /// <summary>
        /// 表情アニメーションかどうかを判定
        /// </summary>
        public static bool IsExpressionAnimation(AnimationClip clip)
        {
            if (clip == null) return false;
            
            // BlendShapeを含むアニメーションかチェック
            var blendShapes = ExtractBlendShapesFromAnimation(clip);
            
            // BlendShapeが含まれており、かつリップシンク用でない場合
            return blendShapes.Count > 0 && 
                   !blendShapes.Any(bs => bs.ToLower().StartsWith("vrc."));
        }
    }
}
