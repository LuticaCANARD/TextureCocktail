using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// AI backend for a local <a href="https://ollama.com">Ollama</a> server.
    ///
    /// Endpoints used:
    ///   GET  /api/tags       — list available models
    ///   POST /api/generate   — generate a response (non-streaming)
    ///
    /// Supports vision models (llava, bakllava, etc.) when an image is attached.
    /// </summary>
    public class OllamaBackend : AiBackendBase
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        public override string DisplayName => "Ollama";
        public override string DefaultServerUrl => "http://localhost:11434";
        public override bool SupportsImageInput => true;

        /// <inheritdoc/>
        public override async Task<List<string>> FetchModelsAsync(string serverUrl, CancellationToken ct = default)
        {
            string url = serverUrl.TrimEnd('/') + "/api/tags";
            string json = await _http.GetStringAsync(url);
            return ParseModelList(json);
        }

        /// <inheritdoc/>
        public override async Task<AiResponse> SendPromptAsync(
            string serverUrl,
            string model,
            AiRequest request,
            CancellationToken ct = default)
        {
            try
            {
                string body = BuildRequestBody(model, request);
                string url = serverUrl.TrimEnd('/') + "/api/generate";

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content, ct);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                return ParseGenerateResponse(responseJson);
            }
            catch (OperationCanceledException)
            {
                return new AiResponse { Success = false, Error = "Request cancelled." };
            }
            catch (Exception ex)
            {
                return new AiResponse { Success = false, Error = ex.Message };
            }
        }

        // ── Request builder ──────────────────────────────────────────────────

        private static string BuildRequestBody(string model, AiRequest request)
        {
            var obj = new JObject
            {
                ["model"] = model,
                ["prompt"] = request.Prompt,
                ["stream"] = false,
            };

            if (request.AttachedImage != null)
            {
                string b64 = AiTextureUtils.TextureToBase64(request.AttachedImage);
                if (!string.IsNullOrEmpty(b64))
                    obj["images"] = new JArray(b64);
            }

            return obj.ToString(Formatting.None);
        }

        // ── Response parsers ─────────────────────────────────────────────────

        private static List<string> ParseModelList(string json)
        {
            var models = new List<string>();
            var root = JObject.Parse(json);
            var arr = root["models"] as JArray;
            if (arr == null) return models;

            foreach (var item in arr)
            {
                string name = item["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    models.Add(name);
            }
            return models;
        }

        private static AiResponse ParseGenerateResponse(string json)
        {
            var root = JObject.Parse(json);
            string text = root["response"]?.ToString() ?? "";

            // Some experimental endpoints include an "images" array in the response
            Texture2D image = null;
            var images = root["images"] as JArray;
            if (images != null && images.Count > 0)
            {
                string b64 = images[0]?.ToString();
                if (!string.IsNullOrEmpty(b64))
                    image = AiTextureUtils.Base64ToTexture(b64);
            }

            return new AiResponse { Success = true, Text = text, Image = image };
        }
    }
}
