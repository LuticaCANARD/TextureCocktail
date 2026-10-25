using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Custom shader controls owned by a TextureCocktail window. Register a concrete
    /// subclass with TextureCocktailShaderAttribute, then override OnGUI and
    /// OnShaderValueChanged. The host creates, initializes and destroys the instance.
    /// </summary>
    public abstract class TextureCocktailContent : EditorWindow
    {
        protected Vector2 scrollPosition;
        protected TextureCocktail baseWindow;

        /// <summary>
        /// Legacy compatibility property. The host does not read this value; custom
        /// Content always draws its own controls through OnGUI. New content need not override it.
        /// </summary>
        public virtual bool UseDefaultLayout { get => false; }

        /// <summary>Shader properties to omit when the Content requests the generic property UI.</summary>
        public virtual string[] DontWantDisplayPropertyName { get; }

        /// <summary>Attaches the owning window before any controls or shader callbacks run.</summary>
        public virtual void Initialize(TextureCocktail baseWindow)
        {
            this.baseWindow = baseWindow;
        }

        /// <summary>
        /// Legacy compatibility hook, including its original spelling. The host never
        /// invokes it. Use OnShaderValueChanged for material updates instead.
        /// </summary>
        public virtual void OnValuepdate() { }

        /// <summary>
        /// Applies Content settings when the host handles a shader-value change.
        /// With ShaderUpdateDefaultAction enabled, the host renders after this callback.
        /// RenderPreview requests made here are coalesced until the callback returns;
        /// CompileShader retains legacy synchronous rendering for extensions that read
        /// PreviewTexture immediately after requesting an update.
        /// </summary>
        public abstract void OnShaderValueChanged();

        /// <summary>Draws the Content controls inside the owning TextureCocktail window.</summary>
        public abstract void OnGUI();

        /// <summary>
        /// True lets the host prepare and render the preview around OnShaderValueChanged.
        /// Legacy overrides returning false must update their preview themselves or request
        /// the shared render path with baseWindow.RenderPreview().
        /// New content should keep the default so shared render steps are applied consistently.
        /// </summary>
        public virtual bool ShaderUpdateDefaultAction { get => true; }

        /// <summary>
        /// Shader pass selected by the Content. A negative value defers to the package's
        /// PassIndex; the legacy default of zero is retained for existing Content subclasses.
        /// </summary>
        public virtual int PassOrder { get => 0; }
    }
}
