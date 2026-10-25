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
    /// AI backend for any server that implements the OpenAI Chat Completions API.
    ///
    /// Compatible products include:
    ///   • LocalAI     (https://localai.io)
    ///   • LM Studio   (https://lmstudio.ai)
    ///   • Jan         (https://jan.ai)
    ///   • Kobold.cpp  (https://github.com/LostRuins/koboldcpp)
    ///   • llama.cpp   server with --api-prefix /v1
    ///   • text-generation-webui with the OpenAI extension
    ///
    /// Endpoints used:
    ///   GET  /v1/models                — list available models
    ///   POST /v1/chat/completions      — generate a response
    ///
    /// Vision input follows the OpenAI vision format (base64 data-URI).
    /// </summary>
    public class OpenAiCompatibleBackend : AiBackendBase
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        public override string DisplayName => "OpenAI-Compatible (LocalAI / LM Studio / Jan …)";
        public override string DefaultServerUrl => "http://localhost:8080";
        public override bool SupportsImageInput => true;

        /// <inheritdoc/>
        public override async Task<List<string>> FetchModelsAsync(string serverUrl, CancellationToken ct = default)
        {
            string url = serverUrl.TrimEnd('/') + "/v1/models";
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
                string url = serverUrl.TrimEnd('/') + "/v1/chat/completions";

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content, ct);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                return ParseChatResponse(responseJson);
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
            // Build the message content — either plain string or mixed array for vision
            JToken messageContent;
            if (request.AttachedImage != null)
            {
                string b64 = AiTextureUtils.TextureToBase64(request.AttachedImage);
                if (!string.IsNullOrEmpty(b64))
                {
                    // OpenAI vision format: content is an array of text + image_url parts
                    messageContent = new JArray(
                        new JObject { ["type"] = "text", ["text"] = request.Prompt },
                        new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = $"data:image/png;base64,{b64}"
                            }
                        }
                    );
                }
                else
                {
                    messageContent = request.Prompt;
                }
            }
            else
            {
                messageContent = request.Prompt;
            }

            var obj = new JObject
            {
                ["model"] = model,
                ["messages"] = new JArray(
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = messageContent,
                    }
                ),
            };

            return obj.ToString(Formatting.None);
        }

        // ── Response parsers ─────────────────────────────────────────────────

        private static List<string> ParseModelList(string json)
        {
            var models = new List<string>();
            var root = JObject.Parse(json);
            var arr = root["data"] as JArray;
            if (arr == null) return models;

            foreach (var item in arr)
            {
                string id = item["id"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                    models.Add(id);
            }
            return models;
        }

        private static AiResponse ParseChatResponse(string json)
        {
            var root = JObject.Parse(json);

            // Standard OpenAI response: choices[0].message.content
            var choices = root["choices"] as JArray;
            if (choices == null || choices.Count == 0)
                return new AiResponse { Success = false, Error = "No choices in response." };

            var message = choices[0]?["message"];
            string text = message?["content"]?.ToString() ?? "";

            // Some custom endpoints return an images array at the top level
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
