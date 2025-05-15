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
    }
}
