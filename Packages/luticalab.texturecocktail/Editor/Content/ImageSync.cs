using LuticaLab.TextureCocktail;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum ImageSyncType
    {
        Add,
        Sub,
        Mul
    }
    public class ImageSync : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { "_BlendOp" }; }

        private ImageSyncType imageSyncType = ImageSyncType.Add;
        private bool _showCompileState = false;
        private bool _isShowPreview = false;

        private static readonly string[] _shaderKeyNames = new string[] { "_BLENDOP_ADD", "_BLENDOP_SUBTRACT", "_BLENDOP_MULTIPLY" };
        private string GetShaderKeyName()
        {
            switch (imageSyncType)
            {
                case ImageSyncType.Add:
                    return _shaderKeyNames[0];
                case ImageSyncType.Sub:
                    return _shaderKeyNames[1];
                case ImageSyncType.Mul:
                    return _shaderKeyNames[2];
                default:
                    return _shaderKeyNames[0]; // Default to Add if unknown
            }
        }
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            try
            {
                GUILayout.Label("Image Sync Content");

                //-----------------
                GUILayout.Label("Sync Type");
                var changed = (ImageSyncType)EditorGUILayout.EnumPopup("Sync Type", imageSyncType);
                switch (imageSyncType)
                {
                    case ImageSyncType.Add:
                        GUILayout.Label("Additive Sync: Combines images by adding pixel values.");
                        break;
                    case ImageSyncType.Sub:
                        GUILayout.Label("Subtractive Sync: Combines images by subtracting pixel values.");
                        break;
                    case ImageSyncType.Mul:
                        GUILayout.Label("Multiplicative Sync: Combines images by multiplying pixel values.");
                        break;
                }
                if (changed != imageSyncType)
                {
                    imageSyncType = changed;
                    string target = GetShaderKeyName();
                    foreach ( var key in _shaderKeyNames)
                    {
                        baseWindow.SetMaterialKeyword(key, key == target);
                    }
                    baseWindow.CompileShader();
                }

                //-----------------

                baseWindow.ShowShaderInfo();

                _showCompileState = EditorGUILayout.BeginFoldoutHeaderGroup(_showCompileState, "Shader Compile State");
                if(_showCompileState)
                    baseWindow.DisplayShaderOptions();// Display shader options if any
                EditorGUILayout.EndFoldoutHeaderGroup();
                _isShowPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_isShowPreview, "Preview");
                if (_isShowPreview)
                {
                    baseWindow.DisplayPassedIamge();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            finally
            {
                GUILayout.EndScrollView();
            }
            
            if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture")))
                baseWindow.SaveTexture();
        }
        public override void OnShaderValueChanged()
        {
            // Handle shader value changes here
        }
    }

}
