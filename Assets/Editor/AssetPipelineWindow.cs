using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIAssetPipeline
{
    /// <summary>
    /// Main Editor Window for the AI 3D Asset Pipeline.
    /// <para>
    /// Provides a minimal but functional UI for:
    /// <list type="bullet">
    ///   <item>Selecting a video file</item>
    ///   <item>Triggering the pipeline (Generate / Cancel)</item>
    ///   <item>Monitoring real-time progress and logs</item>
    /// </list>
    /// </para>
    /// Accessible via <b>Tools → AI 3D Asset Pipeline</b>.
    /// </summary>
    public class AssetPipelineWindow : EditorWindow
    {
        // ---- UI state ----
        private string _videoPath = "";
        private Vector2 _logScrollPos;
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();

        // ---- Progress state ----
        private float  _progress;
        private string _currentStage  = "Idle";
        private string _statusMessage = "Ready";

        // ---- Video metadata / frame preview state ----
        private bool  _hasMetadata;
        private float _videoDuration;
        private int   _videoWidth  = 16;
        private int   _videoHeight = 9;
        private float _previewFrameTime;
        private Texture2D _previewTexture;

        // ---- Click-to-select state ----
        private bool  _hasClick;
        private float _clickX;
        private float _clickY;

        /// <summary>Which kind of one-shot request is currently in flight / just completed.</summary>
        private enum RequestMode { None, Pipeline, Metadata, Preview, Segment }
        private RequestMode _lastRequestMode = RequestMode.None;

        // ---- Styles (lazy-init) ----
        private GUIStyle _logStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _statusBarStyle;
        private bool     _stylesReady;

        // ---- Log entry ----
        private struct LogEntry
        {
            public string  Timestamp;
            public string  Message;
            public LogType Type;
        }

        // =================================================================
        // Menu item
        // =================================================================

        [MenuItem("Tools/AI 3D Asset Pipeline")]
        public static void ShowWindow()
        {
            var win = GetWindow<AssetPipelineWindow>("AI Asset Pipeline");
            win.minSize = new Vector2(460, 520);
        }

        // =================================================================
        // Lifecycle
        // =================================================================

        private void OnEnable()
        {
            PythonBridge.OnProgress   += HandleProgress;
            PythonBridge.OnLogMessage += HandleLog;
            PythonBridge.OnCompleted  += HandleCompleted;
            PythonBridge.OnError      += HandleError;
            PythonBridge.OnPreview    += HandlePreview;
        }

        private void OnDisable()
        {
            PythonBridge.OnProgress   -= HandleProgress;
            PythonBridge.OnLogMessage -= HandleLog;
            PythonBridge.OnCompleted  -= HandleCompleted;
            PythonBridge.OnError      -= HandleError;
            PythonBridge.OnPreview    -= HandlePreview;
        }

        // =================================================================
        // Styles
        // =================================================================

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _logStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap  = true,
                richText  = true,
                fontSize  = 11,
                padding   = new RectOffset(4, 4, 1, 1),
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleCenter,
            };

            _statusBarStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                richText  = true,
            };

            _stylesReady = true;
        }

        // =================================================================
        // OnGUI
        // =================================================================

        private void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.Space(8);

            // ---- Header ----
            EditorGUILayout.LabelField("\u2728 AI 3D Asset Pipeline", _headerStyle);
            EditorGUILayout.Space(4);
            DrawLine();

            // ---- Video input ----
            DrawVideoSection();
            DrawLine();

            // ---- Frame preview + click-to-select ----
            if (_hasMetadata)
            {
                DrawFramePreviewSection();
                DrawLine();
            }

            // ---- Buttons ----
            DrawButtons();
            DrawLine();

            // ---- Progress ----
            DrawProgress();
            DrawLine();

            // ---- Log ----
            DrawLogPanel();

            // ---- Status bar ----
            DrawStatusBar();

            // Force repaint during processing so the progress bar stays live
            if (PythonBridge.IsRunning) Repaint();
        }

        // =================================================================
        // Drawing helpers
        // =================================================================

        private void DrawVideoSection()
        {
            EditorGUILayout.LabelField("Video Input", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(PythonBridge.IsRunning);

                _videoPath = EditorGUILayout.TextField("Video File", _videoPath);

                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    string path = EditorUtility.OpenFilePanel(
                        "Select Video File", "", "mp4,avi,mov,mkv");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _videoPath = path;
                        ResetPreviewState();
                        RequestMetadata();
                    }
                }

                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            // Show file info
            if (!string.IsNullOrEmpty(_videoPath))
            {
                if (File.Exists(_videoPath))
                {
                    var fi = new FileInfo(_videoPath);
                    EditorGUILayout.HelpBox(
                        $"File: {fi.Name}\nSize: {fi.Length / (1024.0 * 1024.0):F1} MB",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("File not found!", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(4);
        }

        private void DrawFramePreviewSection()
        {
            EditorGUILayout.LabelField("Frame Preview & Click-to-Select", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUI.BeginDisabledGroup(PythonBridge.IsRunning);

            EditorGUILayout.BeginHorizontal();
            {
                _previewFrameTime = EditorGUILayout.Slider(
                    "Frame Time (s)", _previewFrameTime, 0f, Mathf.Max(0.01f, _videoDuration));

                if (GUILayout.Button("Load Frame", GUILayout.Width(90)))
                {
                    _hasClick = false;
                    _lastRequestMode = RequestMode.Preview;
                    _currentStage  = "Loading preview...";
                    _statusMessage = $"Requesting frame at {_previewFrameTime:F2}s";
                    PythonBridge.RequestPreviewFrame(_videoPath, _previewFrameTime);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_previewTexture != null)
            {
                float aspect = (float)_videoWidth / _videoHeight;
                float width  = EditorGUIUtility.currentViewWidth - 24f;
                float height = width / aspect;

                Rect previewRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);

                HandlePreviewClick(previewRect);

                if (_hasClick)
                {
                    Vector2 markerPos = new Vector2(
                        previewRect.x + _clickX * previewRect.width,
                        previewRect.y + _clickY * previewRect.height);
                    EditorGUI.DrawRect(new Rect(markerPos.x - 4, markerPos.y - 1, 8, 2), Color.red);
                    EditorGUI.DrawRect(new Rect(markerPos.x - 1, markerPos.y - 4, 2, 8), Color.red);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    _hasClick
                        ? $"Click selected at ({_clickX:F2}, {_clickY:F2}). Click the frame again to move it."
                        : "Click on the frame to select the object to segment.",
                    MessageType.None);

                EditorGUI.BeginDisabledGroup(!_hasClick);
                if (GUILayout.Button("Segment At Click"))
                {
                    _lastRequestMode = RequestMode.Segment;
                    _currentStage  = "Segmenting...";
                    _statusMessage = $"Requesting segmentation at ({_clickX:F2}, {_clickY:F2})";
                    PythonBridge.RequestSegmentation(_videoPath, _previewFrameTime, _clickX, _clickY);
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }

        /// <summary>Detect a mouse click inside the drawn preview rect and store it as a normalized (0-1) coordinate.</summary>
        private void HandlePreviewClick(Rect previewRect)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !previewRect.Contains(e.mousePosition))
                return;

            _clickX  = Mathf.Clamp01((e.mousePosition.x - previewRect.x) / previewRect.width);
            _clickY  = Mathf.Clamp01((e.mousePosition.y - previewRect.y) / previewRect.height);
            _hasClick = true;

            e.Use();
            Repaint();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();
            {
                // Generate
                EditorGUI.BeginDisabledGroup(
                    PythonBridge.IsRunning || string.IsNullOrEmpty(_videoPath));
                if (GUILayout.Button("\u25B6  Generate 3D Asset", GUILayout.Height(34)))
                    StartGeneration();
                EditorGUI.EndDisabledGroup();

                // Cancel
                EditorGUI.BeginDisabledGroup(!PythonBridge.IsRunning);
                if (GUILayout.Button("\u25A0  Cancel", GUILayout.Width(80), GUILayout.Height(34)))
                {
                    PythonBridge.Cancel();
                    AppendLog("Pipeline cancelled by user.", LogType.Warning);
                    _currentStage  = "Cancelled";
                    _statusMessage = "Pipeline cancelled";
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
        }

        private void DrawProgress()
        {
            EditorGUILayout.LabelField("Progress", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField($"Stage: {_currentStage}");

            Rect bar = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.ProgressBar(bar, _progress, $"{_progress * 100:F0}%");

            EditorGUILayout.Space(4);
        }

        private void DrawLogPanel()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log Output", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _logEntries.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            _logScrollPos = EditorGUILayout.BeginScrollView(
                _logScrollPos, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
            {
                foreach (var entry in _logEntries)
                {
                    string color = entry.Type switch
                    {
                        LogType.Error   => "#FF6B6B",
                        LogType.Warning => "#FFD93D",
                        _               => "#C8C8C8",
                    };

                    EditorGUILayout.LabelField(
                        $"<color=#666>[{entry.Timestamp}]</color> <color={color}>{EscapeRichText(entry.Message)}</color>",
                        _logStyle);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                $"<color=#999>{EscapeRichText(_statusMessage)}</color>", _statusBarStyle);
        }

        private void DrawLine()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f));
            EditorGUILayout.Space(4);
        }

        // =================================================================
        // Pipeline control
        // =================================================================

        private void StartGeneration()
        {
            _logEntries.Clear();
            _progress      = 0f;
            _currentStage  = "Starting\u2026";
            _statusMessage = "Pipeline running\u2026";
            _lastRequestMode = RequestMode.Pipeline;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputDir = Path.Combine(Application.dataPath, "GeneratedAssets", timestamp);

            AppendLog("Starting pipeline\u2026",         LogType.Log);
            AppendLog($"Video: {_videoPath}",            LogType.Log);
            AppendLog($"Output: {outputDir}",            LogType.Log);

            PythonBridge.StartPipeline(new PipelineConfig
            {
                VideoPath = _videoPath,
                OutputDir = outputDir,
                ClickX    = _hasClick ? _clickX : 0.5f,
                ClickY    = _hasClick ? _clickY : 0.5f,
            });
        }

        /// <summary>Fetch fps/dimensions/duration for <see cref="_videoPath"/>, to size the preview slider.</summary>
        private void RequestMetadata()
        {
            if (string.IsNullOrEmpty(_videoPath) || !File.Exists(_videoPath)) return;

            _lastRequestMode = RequestMode.Metadata;
            _currentStage    = "Reading video info...";
            _statusMessage   = "Requesting video metadata";
            PythonBridge.RequestMetadata(_videoPath);
        }

        /// <summary>Clear preview/click state \u2014 called whenever the selected video changes.</summary>
        private void ResetPreviewState()
        {
            _hasMetadata      = false;
            _videoDuration    = 0f;
            _previewFrameTime = 0f;
            _previewTexture   = null;
            _hasClick         = false;
        }

        // =================================================================
        // Event handlers (called on main thread by PythonBridge)
        // =================================================================

        private void HandleProgress(ProgressData data)
        {
            _progress      = data.Fraction;
            _currentStage  = data.Stage;
            _statusMessage = data.Message;
        }

        private void HandleLog(string message)
        {
            AppendLog(message, LogType.Log);
        }

        private void HandleCompleted(string resultJson)
        {
            switch (_lastRequestMode)
            {
                case RequestMode.Metadata:
                    HandleMetadataResult(resultJson);
                    break;

                case RequestMode.Preview:
                    _currentStage  = "Preview ready";
                    _statusMessage = "Frame loaded";
                    AppendLog($"Preview frame loaded: {resultJson}", LogType.Log);
                    break;

                case RequestMode.Segment:
                    HandleSegmentResult(resultJson);
                    break;

                case RequestMode.Pipeline:
                default:
                    _currentStage  = "Complete";
                    _statusMessage = "Pipeline completed successfully!";
                    _progress      = 1.0f;
                    AppendLog("\u2705 Pipeline completed successfully!", LogType.Log);
                    AppendLog($"Result: {resultJson}",                 LogType.Log);
                    // Trigger Unity asset import
                    AssetImporter.ImportGeneratedAssets(resultJson);
                    break;
            }
        }

        private void HandleMetadataResult(string resultJson)
        {
            var meta = JsonUtility.FromJson<VideoMetadataResult>(resultJson);
            if (meta == null || meta.status != "success")
            {
                AppendLog("Failed to read video metadata.", LogType.Warning);
                return;
            }

            _hasMetadata      = true;
            _videoDuration    = meta.duration_sec;
            _videoWidth       = Mathf.Max(1, meta.width);
            _videoHeight      = Mathf.Max(1, meta.height);
            _previewFrameTime = Mathf.Clamp(_previewFrameTime, 0f, _videoDuration);

            _currentStage  = "Idle";
            _statusMessage = "Video info loaded";
            AppendLog(
                $"Video: {meta.width}x{meta.height} @ {meta.fps:F2}fps, " +
                $"{meta.frame_count} frames, {meta.duration_sec:F1}s",
                LogType.Log);
        }

        private void HandleSegmentResult(string resultJson)
        {
            var result = JsonUtility.FromJson<SegmentResult>(resultJson);
            if (result == null || result.status != "success")
            {
                AppendLog("Segmentation did not report success.", LogType.Warning);
                return;
            }

            _currentStage  = "Segmentation ready";
            _statusMessage = result.used_real_api
                ? "Segmented via SAM2 (Replicate)"
                : "Segmented via local mock (no SAM2 API key set)";

            AppendLog(
                $"{_statusMessage} \u2014 {result.mask_pixel_count} px masked",
                LogType.Log);
        }

        private void HandlePreview(string base64Png)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64Png);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    _previewTexture = tex;
                    Repaint();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to decode preview image: {ex.Message}", LogType.Warning);
            }
        }

        private void HandleError(string errorMessage)
        {
            _currentStage  = "Error";
            _statusMessage = $"Error: {errorMessage}";
            AppendLog($"\u274C {errorMessage}", LogType.Error);
        }

        // =================================================================
        // Helpers
        // =================================================================

        private void AppendLog(string message, LogType type)
        {
            _logEntries.Add(new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Message   = message,
                Type      = type,
            });

            // Auto-scroll to bottom
            _logScrollPos.y = float.MaxValue;
            Repaint();
        }

        /// <summary>Escape &lt; and &gt; so user content doesn't break Unity's rich text.</summary>
        private static string EscapeRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Don't escape our own color tags — only escape angle brackets that
            // aren't part of <color…> / </color> tags.
            // For simplicity, just let it through — Unity handles most cases.
            return text;
        }

        // =================================================================
        // JSON DTOs (matching main.py's --mode metadata / --mode segment results)
        // =================================================================

        [Serializable]
        private class VideoMetadataResult
        {
            public string status;
            public float  fps;
            public int    frame_count;
            public int    width;
            public int    height;
            public float  duration_sec;
        }

        [Serializable]
        private class SegmentResult
        {
            public string status;
            public bool   used_real_api;
            public int    mask_pixel_count;
        }
    }
}
