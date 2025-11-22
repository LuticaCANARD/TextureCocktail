using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Image viewer window that displays textures at full size or scaled to fit
    /// </summary>
    public class ImageViewerWindow : EditorWindow
    {
        private Texture2D _texture;
        private RenderTexture _renderTexture;
        private Vector2 _scrollPosition;
        private float _zoom = 1.0f;
        private bool _fitToWindow = true;
        private string _textureName;
        
        public static void ShowWindow(Texture2D texture, string name = "Image")
        {
            var window = GetWindow<ImageViewerWindow>(name);
            window.SetTexture(texture);
            window.Show();
        }
        
        public static void ShowWindow(RenderTexture renderTexture, string name = "Preview")
        {
            var window = GetWindow<ImageViewerWindow>(name);
            window.SetRenderTexture(renderTexture);
            window.Show();
        }
        
        public void SetTexture(Texture2D texture)
        {
            _texture = texture;
            _renderTexture = null;
            _textureName = texture != null ? texture.name : "Image";
            titleContent = new GUIContent(_textureName);
            
            if (_fitToWindow && texture != null)
            {
                // Set initial window size based on texture
                float width = Mathf.Min(texture.width, 1200);
                float height = Mathf.Min(texture.height + 80, 800); // Add space for controls
                minSize = new Vector2(400, 300);
                position = new Rect(position.x, position.y, width, height);
            }
        }
        
        public void SetRenderTexture(RenderTexture renderTexture)
        {
            _renderTexture = renderTexture;
            _texture = null;
            _textureName = "Preview";
            titleContent = new GUIContent(_textureName);
            
            if (_fitToWindow && renderTexture != null)
            {
                // Set initial window size based on render texture
                float width = Mathf.Min(renderTexture.width, 1200);
                float height = Mathf.Min(renderTexture.height + 80, 800);
                minSize = new Vector2(400, 300);
                position = new Rect(position.x, position.y, width, height);
            }
        }
        
        private void OnGUI()
        {
            Texture displayTexture = _renderTexture != null ? (Texture)_renderTexture : (Texture)_texture;
            
            if (displayTexture == null)
            {
                EditorGUILayout.HelpBox(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("no_image_to_display"), 
                    MessageType.Info
                );
                return;
            }
            
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Fit to window toggle
            var newFit = GUILayout.Toggle(_fitToWindow, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("fit_to_window"), 
                EditorStyles.toolbarButton, GUILayout.Width(100));
            if (newFit != _fitToWindow)
            {
                _fitToWindow = newFit;
                if (_fitToWindow)
                {
                    _zoom = 1.0f;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // Zoom controls (only visible when not fitting to window)
            if (!_fitToWindow)
            {
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("zoom"), GUILayout.Width(40));
                
                if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
                {
                    _zoom = Mathf.Max(0.1f, _zoom - 0.1f);
                }
                
                _zoom = GUILayout.HorizontalSlider(_zoom, 0.1f, 4.0f, GUILayout.Width(100));
                
                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
                {
                    _zoom = Mathf.Min(4.0f, _zoom + 0.1f);
                }
                
                GUILayout.Label($"{(_zoom * 100):F0}%", EditorStyles.toolbarButton, GUILayout.Width(50));
                
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("reset_zoom"), 
                    EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _zoom = 1.0f;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // Image info
            GUILayout.Label($"{displayTexture.width} x {displayTexture.height}", 
                EditorStyles.toolbarButton, GUILayout.Width(100));
            
            EditorGUILayout.EndHorizontal();
            
            // Image display area
            Rect imageRect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            if (_fitToWindow)
            {
                // Calculate scale to fit the image in the available space
                float scaleX = imageRect.width / displayTexture.width;
                float scaleY = imageRect.height / displayTexture.height;
                float scale = Mathf.Min(scaleX, scaleY);
                
                float scaledWidth = displayTexture.width * scale;
                float scaledHeight = displayTexture.height * scale;
                
                // Center the image
                float x = imageRect.x + (imageRect.width - scaledWidth) * 0.5f;
                float y = imageRect.y + (imageRect.height - scaledHeight) * 0.5f;
                
                Rect centeredRect = new Rect(x, y, scaledWidth, scaledHeight);
                GUI.DrawTexture(centeredRect, displayTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                // Use scrollable area with zoom
                _scrollPosition = GUI.BeginScrollView(imageRect, _scrollPosition, 
                    new Rect(0, 0, displayTexture.width * _zoom, displayTexture.height * _zoom));
                
                GUI.DrawTexture(new Rect(0, 0, displayTexture.width * _zoom, displayTexture.height * _zoom), 
                    displayTexture, ScaleMode.StretchToFill);
                
                GUI.EndScrollView();
            }
            
            // Instructions at the bottom
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("image_viewer_help"), 
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
    }
}
