using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Multi-backend AI connector editor window.
    ///
    /// Open via: LuticaLab → AI Connector
    ///
    /// INPUT  : text prompt  +  (optional) Texture2D for vision models
    /// OUTPUT : AI-generated text (and an image preview when the response contains one)
    ///
    /// Supported backends:
    ///   • Ollama                  — http://localhost:11434
    ///   • OpenAI-compatible APIs  — LocalAI, LM Studio, Jan, Kobold.cpp, llama.cpp, etc.
    ///
    /// Add more backends by creating a class that inherits <see cref="AiBackendBase"/>
    /// anywhere in any loaded assembly — the window discovers them automatically.
    /// </summary>
    public class OllamaConnector : EditorWindow
    {
        // ── Menu item ────────────────────────────────────────────────────────
        [MenuItem("LuticaLab/AI Connector")]
        public static void ShowWindow()
        {
            GetWindow<OllamaConnector>("AI Connector");
        }

        // ── Known backends ───────────────────────────────────────────────────
        private static readonly AiBackendBase[] Backends = new AiBackendBase[]
        {
            new OllamaBackend(),
            new OpenAiCompatibleBackend(),
        };

        // ── EditorPrefs keys ─────────────────────────────────────────────────
        private const string PrefsKeyBackend = "TC_AI_Backend";
        private const string PrefsKeyUrl = "TC_AI_Url";
        private const string PrefsKeyModel = "TC_AI_Model";

        // ── State ────────────────────────────────────────────────────────────
        private int _backendIndex = 0;
        private string _serverUrl = "";
        private string _selectedModel = "";
        private List<string> _availableModels = new List<string>();
        private int _selectedModelIndex = 0;

        private string _promptText = "";
        private Texture2D _inputTexture;
        private bool _attachTexture;

        private string _responseText = "";
        private Texture2D _responseImage;
        private Vector2 _responseScroll;

        private bool _busy;
        private string _statusMessage = "";
        private CancellationTokenSource _cts;

        private AiBackendBase ActiveBackend => Backends[_backendIndex];

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _backendIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefsKeyBackend, 0), 0, Backends.Length - 1);
            _serverUrl = EditorPrefs.GetString(PrefsKeyUrl, ActiveBackend.DefaultServerUrl);
            _selectedModel = EditorPrefs.GetString(PrefsKeyModel, "");
            if (string.IsNullOrEmpty(_statusMessage))
                _statusMessage = "Ready. Select a backend, configure the server URL, and click 'List Models'.";
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            EditorPrefs.SetInt(PrefsKeyBackend, _backendIndex);
            EditorPrefs.SetString(PrefsKeyUrl, _serverUrl);
            EditorPrefs.SetString(PrefsKeyModel, _selectedModel);
        }

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            GUILayout.Label("AI Connector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Local AI pipeline: text prompt (+ optional image) → AI-generated text response.\n" +
                "Supports Ollama and any OpenAI-compatible API (LocalAI, LM Studio, Jan, Kobold…).",
                MessageType.Info);

            EditorGUILayout.Space(4);
            DrawBackendSection();
            EditorGUILayout.Space(4);
            DrawPromptSection();
            EditorGUILayout.Space(4);
            DrawResponseSection();
            EditorGUILayout.Space(4);
            DrawStatusBar();
        }

        // ── Backend / server section ─────────────────────────────────────────
        private void DrawBackendSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Backend & Server", EditorStyles.boldLabel);

            // Backend selector
            string[] backendNames = new string[Backends.Length];
            for (int i = 0; i < Backends.Length; i++)
                backendNames[i] = Backends[i].DisplayName;

            int newBackendIdx = EditorGUILayout.Popup("Backend", _backendIndex, backendNames);
            if (newBackendIdx != _backendIndex)
            {
                _backendIndex = newBackendIdx;
                // Pre-fill the default URL for the newly selected backend
                _serverUrl = ActiveBackend.DefaultServerUrl;
                _availableModels.Clear();
                _selectedModel = "";
            }

            // Server URL + List Models
            EditorGUILayout.BeginHorizontal();
            _serverUrl = EditorGUILayout.TextField("Server URL", _serverUrl);
            GUI.enabled = !_busy;
            if (GUILayout.Button("List Models", GUILayout.Width(100)))
                _ = FetchModelsAsync();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Model selector
            if (_availableModels.Count > 0)
            {
                _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _availableModels.Count - 1);
                int newIdx = EditorGUILayout.Popup("Model", _selectedModelIndex, _availableModels.ToArray());
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

            // Image attachment — only shown for backends that support it
            if (ActiveBackend.SupportsImageInput)
            {
                _attachTexture = EditorGUILayout.Toggle("Attach Texture (vision models)", _attachTexture);
                if (_attachTexture)
                {
                    _inputTexture = (Texture2D)EditorGUILayout.ObjectField(
                        "Input Texture", _inputTexture, typeof(Texture2D), false);

                    if (_inputTexture != null)
                    {
                        Rect thumbRect = GUILayoutUtility.GetRect(80, 80);
                        GUI.DrawTexture(thumbRect, _inputTexture, ScaleMode.ScaleToFit);
                    }

                    EditorGUILayout.HelpBox(
                        "Requires a vision model (e.g. llava for Ollama, or a multimodal model for OpenAI-compatible backends). " +
                        "The texture is converted to PNG and sent as base64.",
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_busy && !string.IsNullOrWhiteSpace(_promptText) && !string.IsNullOrEmpty(_selectedModel);
            if (GUILayout.Button("Send Prompt", GUILayout.Height(32)))
                _ = SendPromptAsync();
            GUI.enabled = _busy;
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

            // Input texture context
            if (_attachTexture && _inputTexture != null && !string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.Space(4);
                GUILayout.Label("Input Image Context:", EditorStyles.boldLabel);
                Rect imgRect = GUILayoutUtility.GetRect(160, 120);
                GUI.DrawTexture(imgRect, _inputTexture, ScaleMode.ScaleToFit);
            }

            // Response image (if the backend returned one)
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
            bool isError = _statusMessage.StartsWith("Error");
            MessageType msgType = isError ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(_statusMessage, msgType);
            if (_busy)
                EditorGUILayout.HelpBox("⏳ Waiting for response…", MessageType.Info);
        }

        // ── Async operations ─────────────────────────────────────────────────

        private async System.Threading.Tasks.Task FetchModelsAsync()
        {
            SetBusy(true, "Fetching model list…");
            try
            {
                var models = await ActiveBackend.FetchModelsAsync(_serverUrl);
                _availableModels = models;

                int idx = _availableModels.IndexOf(_selectedModel);
                _selectedModelIndex = idx >= 0 ? idx : 0;
                if (_availableModels.Count > 0)
                    _selectedModel = _availableModels[_selectedModelIndex];

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

        private async System.Threading.Tasks.Task SendPromptAsync()
        {
            if (string.IsNullOrWhiteSpace(_promptText) || string.IsNullOrEmpty(_selectedModel))
                return;

            _cts = new CancellationTokenSource();
            SetBusy(true, "Sending prompt…");
            _responseText = "";
            _responseImage = null;

            var request = new AiRequest
            {
                Prompt = _promptText,
                AttachedImage = (_attachTexture && ActiveBackend.SupportsImageInput) ? _inputTexture : null,
            };

            AiResponse result = await ActiveBackend.SendPromptAsync(_serverUrl, _selectedModel, request, _cts.Token);

            if (result.Success)
            {
                _responseText = result.Text;
                _responseImage = result.Image;
                SetStatus("Response received.");
            }
            else
            {
                SetStatus($"Error: {result.Error}");
            }

            SetBusy(false);
            EditorApplication.delayCall += Repaint;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SaveResponseImage()
        {
            if (_responseImage == null) return;
            string path = EditorUtility.SaveFilePanel("Save Response Image", "Assets", "ai_response.png", "png");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, _responseImage.EncodeToPNG());
                AssetDatabase.Refresh();
                SetStatus($"Image saved to {path}");
            }
        }

        private void SetBusy(bool busy, string msg = null)
        {
            _busy = busy;
            if (msg != null) _statusMessage = msg;
            EditorApplication.delayCall += Repaint;
        }

        private void SetStatus(string msg)
        {
            _statusMessage = msg;
            EditorApplication.delayCall += Repaint;
        }
    }
}
