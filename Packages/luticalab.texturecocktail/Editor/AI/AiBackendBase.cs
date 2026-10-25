using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Payload sent to an AI backend.
    /// </summary>
    public struct AiRequest
    {
        /// <summary>Text prompt to send.</summary>
        public string Prompt;

        /// <summary>Optional image attachment for vision models. May be <c>null</c>.</summary>
        public Texture2D AttachedImage;
    }

    /// <summary>
    /// Response received from an AI backend.
    /// </summary>
    public struct AiResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success;

        /// <summary>The text portion of the response.</summary>
        public string Text;

        /// <summary>Optional decoded image returned in the response. May be <c>null</c>.</summary>
        public Texture2D Image;

        /// <summary>Error message when <see cref="Success"/> is <c>false</c>.</summary>
        public string Error;
    }

    /// <summary>
    /// Abstract base class for local / remote AI backends.
    ///
    /// Implement this to add a new AI provider.  The <see cref="OllamaConnector"/>
    /// discovers all concrete subclasses at runtime and presents them in a dropdown.
    ///
    /// Implementations live in <c>Editor/AI/</c> — see <see cref="OllamaBackend"/> and
    /// <see cref="OpenAiCompatibleBackend"/> for reference examples.
    /// </summary>
    public abstract class AiBackendBase
    {
        /// <summary>Human-readable name shown in the backend selector.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Default server URL pre-filled in the UI.</summary>
        public abstract string DefaultServerUrl { get; }

        /// <summary>
        /// Whether this backend can accept an image alongside the text prompt.
        /// When <c>false</c> the image attachment UI is hidden for this backend.
        /// </summary>
        public abstract bool SupportsImageInput { get; }

        /// <summary>
        /// Returns the list of model names available on the server.
        /// Throw on network failure so the caller can surface the error.
        /// </summary>
        public abstract Task<List<string>> FetchModelsAsync(
            string serverUrl,
            CancellationToken ct = default);

        /// <summary>
        /// Sends a prompt and returns the response.
        /// The implementation must not throw — return <see cref="AiResponse.Success"/> = false
        /// with a populated <see cref="AiResponse.Error"/> instead.
        /// </summary>
        public abstract Task<AiResponse> SendPromptAsync(
            string serverUrl,
            string model,
            AiRequest request,
            CancellationToken ct = default);
    }
}
