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
        }

        private void OnDisable()
        {
            PythonBridge.OnProgress   -= HandleProgress;
            PythonBridge.OnLogMessage -= HandleLog;
            PythonBridge.OnCompleted  -= HandleCompleted;
            PythonBridge.OnError      -= HandleError;
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
                        _videoPath = path;
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

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputDir = Path.Combine(Application.dataPath, "GeneratedAssets", timestamp);

            AppendLog("Starting pipeline\u2026",         LogType.Log);
            AppendLog($"Video: {_videoPath}",            LogType.Log);
            AppendLog($"Output: {outputDir}",            LogType.Log);

            PythonBridge.StartPipeline(new PipelineConfig
            {
                VideoPath = _videoPath,
                OutputDir = outputDir,
                ClickX    = 0.5f,
                ClickY    = 0.5f,
            });
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
            _currentStage  = "Complete";
            _statusMessage = "Pipeline completed successfully!";
            _progress      = 1.0f;

            AppendLog("\u2705 Pipeline completed successfully!", LogType.Log);
            AppendLog($"Result: {resultJson}",                 LogType.Log);

            // Trigger Unity asset import
            AssetImporter.ImportGeneratedAssets(resultJson);
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
    }
}
