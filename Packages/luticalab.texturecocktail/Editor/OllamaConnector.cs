using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Editor window that connects to a local Ollama instance and provides a
    /// text → text+image AI pipeline inside Unity Editor.
    ///
    /// Open via: LuticaLab → Ollama AI Connector
    ///
    /// INPUT  : text prompt  +  (optional) Texture2D for vision models
    /// OUTPUT : text response displayed in the window
    ///
    /// Requires a running Ollama server (default: http://localhost:11434).
    /// Install Ollama at https://ollama.com and pull a model, e.g.:
    ///   ollama pull llama3
    ///   ollama pull llava   (for vision / image input)
    /// </summary>
    public class OllamaConnector : EditorWindow
    {
        // ── Menu item ────────────────────────────────────────────────────────
        [MenuItem("LuticaLab/Ollama AI Connector")]
        public static void ShowWindow()
        {
            GetWindow<OllamaConnector>("Ollama AI");
        }

        // ── Constants ────────────────────────────────────────────────────────
        private const string DefaultServerUrl = "http://localhost:11434";
        private const string PrefsKeyUrl = "TC_Ollama_Url";
        private const string PrefsKeyModel = "TC_Ollama_Model";

        // ── State ────────────────────────────────────────────────────────────
        private string _serverUrl = DefaultServerUrl;
        private string _selectedModel = "";
        private List<string> _availableModels = new List<string>();
        private int _selectedModelIndex = 0;

        private string _promptText = "";
        private Texture2D _inputTexture;
        private bool _attachTexture;

        private string _responseText = "";
        private Texture2D _responseImage;   // decoded from base64 if server returns one
        private Vector2 _responseScroll;

        private bool _busy;
        private string _statusMessage = "Ready. Configure server URL and click 'List Models'.";
        private CancellationTokenSource _cts;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _serverUrl = EditorPrefs.GetString(PrefsKeyUrl, DefaultServerUrl);
            _selectedModel = EditorPrefs.GetString(PrefsKeyModel, "");
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            EditorPrefs.SetString(PrefsKeyUrl, _serverUrl);
            EditorPrefs.SetString(PrefsKeyModel, _selectedModel);
        }

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            GUILayout.Label("Ollama Local AI Connector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Connects to a local Ollama server. Input: text prompt (+ optional image). " +
                "Output: AI-generated text (and image preview when an image was provided).",
                MessageType.Info);

            EditorGUILayout.Space(4);
            DrawServerSection();
            EditorGUILayout.Space(4);
            DrawPromptSection();
            EditorGUILayout.Space(4);
            DrawResponseSection();
            EditorGUILayout.Space(4);
            DrawStatusBar();
        }

        // ── Server section ───────────────────────────────────────────────────
        private void DrawServerSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Server Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _serverUrl = EditorGUILayout.TextField("Ollama URL", _serverUrl);
            GUI.enabled = !_busy;
            if (GUILayout.Button("List Models", GUILayout.Width(100)))
                _ = FetchModelsAsync();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_availableModels.Count > 0)
            {
                string[] modelArray = _availableModels.ToArray();
                _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, modelArray.Length - 1);
                int newIdx = EditorGUILayout.Popup("Model", _selectedModelIndex, modelArray);
                if (newIdx != _selectedModelIndex)
                {
                    _selectedModelIndex = newIdx;
                    _selectedModel = _availableModels[newIdx];
                }
            }
            else
            {
                _selectedModel = EditorGUILayout.TextField("Model (manual)", _selectedModel);
            }

            EditorGUILayout.EndVertical();
        }

        // ── Prompt section ───────────────────────────────────────────────────
        private void DrawPromptSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Prompt", EditorStyles.boldLabel);

            GUILayout.Label("Text Prompt:");
            _promptText = EditorGUILayout.TextArea(_promptText, GUILayout.MinHeight(80));

            EditorGUILayout.Space(4);

            // Image attachment (for vision models like llava)
            _attachTexture = EditorGUILayout.Toggle("Attach Texture (vision models)", _attachTexture);
            if (_attachTexture)
            {
                _inputTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Input Texture", _inputTexture, typeof(Texture2D), false);

                if (_inputTexture != null)
                {
                    // Preview thumbnail
                    Rect thumbRect = GUILayoutUtility.GetRect(80, 80);
                    GUI.DrawTexture(thumbRect, _inputTexture, ScaleMode.ScaleToFit);
                }

                EditorGUILayout.HelpBox(
                    "Requires a vision model (e.g. llava). The texture is converted to PNG and " +
                    "sent as base64. Make sure the texture has Read/Write enabled in its import settings.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_busy && !string.IsNullOrWhiteSpace(_promptText) && !string.IsNullOrEmpty(_selectedModel);
            if (GUILayout.Button("Send Prompt", GUILayout.Height(32)))
                _ = SendPromptAsync();
            GUI.enabled = !_busy;
            if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(32)))
                _cts?.Cancel();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ── Response section ─────────────────────────────────────────────────
        private void DrawResponseSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Response", EditorStyles.boldLabel);

            _responseScroll = EditorGUILayout.BeginScrollView(_responseScroll, GUILayout.MinHeight(120));
            if (!string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.SelectableLabel(_responseText,
                    EditorStyles.wordWrappedLabel,
                    GUILayout.ExpandHeight(true));
            }
            else
            {
                GUILayout.Label("(no response yet)", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();

            // If input texture was attached, show a combined image+text panel
            if (_attachTexture && _inputTexture != null && !string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.Space(4);
                GUILayout.Label("Input Image Context:", EditorStyles.boldLabel);
                Rect imgRect = GUILayoutUtility.GetRect(160, 120);
                GUI.DrawTexture(imgRect, _inputTexture, ScaleMode.ScaleToFit);
            }

            // If the response contained a base64 image, show it
            if (_responseImage != null)
            {
                EditorGUILayout.Space(4);
                GUILayout.Label("Response Image:", EditorStyles.boldLabel);
                Rect imgRect = GUILayoutUtility.GetRect(200, 200);
                GUI.DrawTexture(imgRect, _responseImage, ScaleMode.ScaleToFit);

                if (GUILayout.Button("Save Response Image…", GUILayout.Width(180)))
                    SaveResponseImage();
            }

            if (!string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear", GUILayout.Width(70)))
                {
                    _responseText = "";
                    _responseImage = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            MessageType msgType = _busy ? MessageType.Info :
                (_statusMessage.StartsWith("Error") ? MessageType.Error : MessageType.Info);
            EditorGUILayout.HelpBox(_statusMessage, msgType);
            if (_busy)
                EditorGUILayout.HelpBox("⏳ Waiting for Ollama response…", MessageType.Info);
        }

        // ── Async helpers ────────────────────────────────────────────────────

        private async Task FetchModelsAsync()
        {
            SetBusy(true, "Fetching model list…");
            try
            {
                string url = _serverUrl.TrimEnd('/') + "/api/tags";
                string json = await _http.GetStringAsync(url);
                ParseModelList(json);
                SetStatus($"Found {_availableModels.Count} model(s).");
            }
            catch (Exception ex)
            {
                SetStatus($"Error fetching models: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SendPromptAsync()
        {
            if (string.IsNullOrWhiteSpace(_promptText) || string.IsNullOrEmpty(_selectedModel))
                return;

            _cts = new CancellationTokenSource();
            SetBusy(true, "Sending prompt…");
            _responseText = "";
            _responseImage = null;

            try
            {
                string body = BuildRequestBody();
                string url = _serverUrl.TrimEnd('/') + "/api/generate";

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content, _cts.Token);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                ParseGenerateResponse(responseJson);

                SetStatus("Response received.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Request cancelled.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── JSON helpers (manual, no extra deps) ─────────────────────────────

        private string BuildRequestBody()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":{JsonString(_selectedModel)},");
            sb.Append($"\"prompt\":{JsonString(_promptText)},");
            sb.Append("\"stream\":false");

            if (_attachTexture && _inputTexture != null)
            {
                string b64 = TextureToBase64(_inputTexture);
                if (!string.IsNullOrEmpty(b64))
                {
                    sb.Append($",\"images\":[{JsonString(b64)}]");
                }
            }

            sb.Append("}");
            return sb.ToString();
        }

        private void ParseModelList(string json)
        {
            _availableModels.Clear();
            // Parse "models":[{"name":"..."},...] — lightweight manual parse
            int modelsIdx = json.IndexOf("\"models\"", StringComparison.Ordinal);
            if (modelsIdx < 0) return;

            int start = json.IndexOf('[', modelsIdx);
            int end = json.IndexOf(']', start);
            if (start < 0 || end < 0) return;

            string segment = json.Substring(start, end - start + 1);
            int pos = 0;
            while (true)
            {
                int nameIdx = segment.IndexOf("\"name\"", pos, StringComparison.Ordinal);
                if (nameIdx < 0) break;
                int colon = segment.IndexOf(':', nameIdx);
                int q1 = segment.IndexOf('"', colon + 1);
                int q2 = segment.IndexOf('"', q1 + 1);
                if (q1 < 0 || q2 < 0) break;
                string name = segment.Substring(q1 + 1, q2 - q1 - 1);
                _availableModels.Add(name);
                pos = q2 + 1;
            }

            // Restore persisted selection
            int idx = _availableModels.IndexOf(_selectedModel);
            _selectedModelIndex = idx >= 0 ? idx : 0;
            if (_availableModels.Count > 0)
                _selectedModel = _availableModels[_selectedModelIndex];
        }

        private void ParseGenerateResponse(string json)
        {
            // Extract "response":"..."
            _responseText = ExtractJsonStringField(json, "response");

            // Some models/endpoints may return "images":["base64..."]
            int imgIdx = json.IndexOf("\"images\"", StringComparison.Ordinal);
            if (imgIdx >= 0)
            {
                int arrStart = json.IndexOf('[', imgIdx);
                int q1 = json.IndexOf('"', arrStart);
                int q2 = json.IndexOf('"', q1 + 1);
                if (q1 >= 0 && q2 > q1)
                {
                    string b64 = json.Substring(q1 + 1, q2 - q1 - 1);
                    _responseImage = Base64ToTexture(b64);
                }
            }

            Repaint();
        }

        private static string ExtractJsonStringField(string json, string fieldName)
        {
            int idx = json.IndexOf($"\"{fieldName}\"", StringComparison.Ordinal);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx);
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            var sb = new StringBuilder();
            bool escape = false;
            for (int i = q1 + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escape)
                {
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        default: sb.Append(c); break;
                    }
                    escape = false;
                }
                else if (c == '\\') { escape = true; }
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string JsonString(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── Texture helpers ──────────────────────────────────────────────────

        private static string TextureToBase64(Texture2D tex)
        {
            try
            {
                // Ensure read/write; if texture is not readable, render it first
                byte[] pngBytes;
                if (tex.isReadable)
                {
                    pngBytes = tex.EncodeToPNG();
                }
                else
                {
                    var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = null;
                    rt.Release();
                    pngBytes = readable.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(readable);
                }
                return Convert.ToBase64String(pngBytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OllamaConnector] Could not encode texture: {ex.Message}");
                return null;
            }
        }

        private static Texture2D Base64ToTexture(string base64)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch
            {
                return null;
            }
        }

        private void SaveResponseImage()
        {
            if (_responseImage == null) return;
            string path = EditorUtility.SaveFilePanel("Save Response Image", "Assets", "ollama_response.png", "png");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllBytes(path, _responseImage.EncodeToPNG());
                AssetDatabase.Refresh();
                SetStatus($"Image saved to {path}");
            }
        }

        // ── Thread-safe UI update helpers ────────────────────────────────────
        private void SetBusy(bool busy, string msg = null)
        {
            _busy = busy;
            if (msg != null) _statusMessage = msg;
            // Repaint must happen on the main thread — use EditorApplication.delayCall
            EditorApplication.delayCall += Repaint;
        }

        private void SetStatus(string msg)
        {
            _statusMessage = msg;
            EditorApplication.delayCall += Repaint;
        }
    }
}
