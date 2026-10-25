using LuticaLab;
using UnityEditor;
using UnityEngine;

namespace Assets.LuticaLab.Core.Editor
{
    public class LuticaLabSetting : EditorWindow
    {
        [MenuItem("LuticaLab/Setting", false, 0)]
        public static void Init()
        {
            LuticaLabSetting window = (LuticaLabSetting)GetWindow(typeof(LuticaLabSetting));
            window.titleContent = new GUIContent(LanguageDisplayer.Instance.GetTranslatedLanguage("Lutica Lab Setting"));
            window.Show();
        }
        private void OnGUI()
        {
            var language = LanguageDisplayer.Instance;
            string title = language.GetTranslatedLanguage("Lutica Lab Setting");
            titleContent = new GUIContent(title);
            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var selected = (LuticaLabSupportLanguage)EditorGUILayout.EnumPopup(
                language.GetTranslatedLanguage("Language"), language.NowLanguage);
            if (EditorGUI.EndChangeCheck()) language.NowLanguage = selected;
        }
    }
  
}