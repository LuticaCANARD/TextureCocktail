using UiText = LuticaLab.TextureCocktail.TextureCocktailLocalization;
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
            GetWindow<OllamaConnector>(UiText.Text("AI Connector"));
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
        private object[] _statusArguments = Array.Empty<object>();
        private bool _statusError;
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
            titleContent.text = UiText.Text("AI Connector");
            GUILayout.Label(UiText.Text("AI Connector"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                UiText.Text("Local AI pipeline: text prompt (+ optional image) → AI-generated text response.\nSupports Ollama and any OpenAI-compatible API (LocalAI, LM Studio, Jan, Kobold…)."),
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
            GUILayout.Label(UiText.Text("Backend & Server"), EditorStyles.boldLabel);

            // Backend selector
            string[] backendNames = new string[Backends.Length];
            for (int i = 0; i < Backends.Length; i++)
                backendNames[i] = Backends[i].DisplayName;

            int newBackendIdx = EditorGUILayout.Popup(UiText.Text("Backend"), _backendIndex, backendNames);
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
            _serverUrl = EditorGUILayout.TextField(UiText.Text("Server URL"), _serverUrl);
            GUI.enabled = !_busy;
            if (GUILayout.Button(UiText.Text("ollama_list_models"), GUILayout.Width(100)))
                _ = FetchModelsAsync();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Model selector
            if (_availableModels.Count > 0)
            {
                _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _availableModels.Count - 1);
                int newIdx = EditorGUILayout.Popup(UiText.Text("ollama_model"), _selectedModelIndex, _availableModels.ToArray());
                if (newIdx != _selectedModelIndex)
                {
                    _selectedModelIndex = newIdx;
                    _selectedModel = _availableModels[newIdx];
                }
            }
            else
            {
                _selectedModel = EditorGUILayout.TextField(UiText.Text("ollama_model_manual"), _selectedModel);
            }

            EditorGUILayout.EndVertical();
        }

        // ── Prompt section ───────────────────────────────────────────────────
        private void DrawPromptSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(UiText.Text("ollama_prompt_section"), EditorStyles.boldLabel);

            GUILayout.Label(UiText.Text("ollama_prompt_label"));
            _promptText = EditorGUILayout.TextArea(_promptText, GUILayout.MinHeight(80));

            EditorGUILayout.Space(4);

            // Image attachment — only shown for backends that support it
            if (ActiveBackend.SupportsImageInput)
            {
                _attachTexture = EditorGUILayout.Toggle(UiText.Text("ollama_attach_texture"), _attachTexture);
                if (_attachTexture)
                {
                    _inputTexture = (Texture2D)EditorGUILayout.ObjectField(
                        UiText.Text("Input Texture"), _inputTexture, typeof(Texture2D), false);

                    if (_inputTexture != null)
                    {
                        Rect thumbRect = GUILayoutUtility.GetRect(80, 80);
                        GUI.DrawTexture(thumbRect, _inputTexture, ScaleMode.ScaleToFit);
                    }

                    EditorGUILayout.HelpBox(
                        UiText.Text("Requires a vision model (e.g. llava for Ollama, or a multimodal model for OpenAI-compatible backends). The texture is converted to PNG and sent as base64."),
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_busy && !string.IsNullOrWhiteSpace(_promptText) && !string.IsNullOrEmpty(_selectedModel);
            if (GUILayout.Button(UiText.Text("ollama_send"), GUILayout.Height(32)))
                _ = SendPromptAsync();
            GUI.enabled = _busy;
            if (GUILayout.Button(UiText.Text("ollama_cancel"), GUILayout.Width(80), GUILayout.Height(32)))
                _cts?.Cancel();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ── Response section ─────────────────────────────────────────────────
        private void DrawResponseSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(UiText.Text("ollama_response_section"), EditorStyles.boldLabel);

            _responseScroll = EditorGUILayout.BeginScrollView(_responseScroll, GUILayout.MinHeight(120));
            if (!string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.SelectableLabel(_responseText,
                    EditorStyles.wordWrappedLabel,
                    GUILayout.ExpandHeight(true));
            }
            else
            {
                GUILayout.Label(UiText.Text("ollama_no_response"), EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();

            // Input texture context
            if (_attachTexture && _inputTexture != null && !string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.Space(4);
                GUILayout.Label(UiText.Text("ollama_input_context"), EditorStyles.boldLabel);
                Rect imgRect = GUILayoutUtility.GetRect(160, 120);
                GUI.DrawTexture(imgRect, _inputTexture, ScaleMode.ScaleToFit);
            }

            // Response image (if the backend returned one)
            if (_responseImage != null)
            {
                EditorGUILayout.Space(4);
                GUILayout.Label(UiText.Text("ollama_response_image"), EditorStyles.boldLabel);
                Rect imgRect = GUILayoutUtility.GetRect(200, 200);
                GUI.DrawTexture(imgRect, _responseImage, ScaleMode.ScaleToFit);

                if (GUILayout.Button(UiText.Text("Save Response Image…"), GUILayout.Width(180)))
                    SaveResponseImage();
            }

            if (!string.IsNullOrEmpty(_responseText))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(UiText.Text("ollama_clear"), GUILayout.Width(70)))
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
            MessageType msgType = _statusError ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(UiText.Format(_statusMessage, _statusArguments), msgType);
            if (_busy)
                EditorGUILayout.HelpBox(UiText.Text("⏳ Waiting for response…"), MessageType.Info);
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

                SetStatus("Found {0} model(s).", false, _availableModels.Count);
            }
            catch (Exception ex)
            {
                SetStatus("Error fetching models: {0}", true, ex.Message);
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
                SetStatus("Error: {0}", true, result.Error);
            }

            SetBusy(false);
            EditorApplication.delayCall += Repaint;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SaveResponseImage()
        {
            if (_responseImage == null) return;
            string path = EditorUtility.SaveFilePanel(UiText.Text("Save Response Image"), "Assets", "ai_response.png", "png");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, _responseImage.EncodeToPNG());
                AssetDatabase.Refresh();
                SetStatus("Image saved to {0}", false, path);
            }
        }

        private void SetBusy(bool busy, string msg = null)
        {
            _busy = busy;
            if (msg != null)
            {
                _statusMessage = msg;
                _statusArguments = Array.Empty<object>();
                _statusError = false;
            }
            EditorApplication.delayCall += Repaint;
        }

        private void SetStatus(string msg, bool isError = false, params object[] arguments)
        {
            _statusMessage = msg;
            _statusArguments = arguments;
            _statusError = isError;
            EditorApplication.delayCall += Repaint;
        }
    }
}
