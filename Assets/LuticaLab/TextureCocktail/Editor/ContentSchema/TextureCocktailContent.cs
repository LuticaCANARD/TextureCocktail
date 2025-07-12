using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace LuticaLab.TextureCocktail
{
    public abstract class TextureCocktailContent : EditorWindow
    {
        protected Vector2 scrollPosition;
        protected TextureCocktail baseWindow;
        public abstract bool UseDefaultLayout { get; }
        public virtual string[] DontWantDisplayPropertyName { get; }
        public virtual void Initialize(TextureCocktail baseWindow)
        {
            this.baseWindow = baseWindow;
        }
        public virtual void OnValuepdate() { }
        public abstract void OnShaderValueChanged();
        public abstract void OnGUI();

    }

}
