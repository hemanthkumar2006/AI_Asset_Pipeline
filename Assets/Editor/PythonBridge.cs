using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AIAssetPipeline
{
    // -------------------------------------------------------------------------
    // Data types
    // -------------------------------------------------------------------------

    /// <summary>
    /// Configuration for a single pipeline run.
    /// </summary>
    [Serializable]
    public class PipelineConfig
    {
        public string VideoPath;
        public string OutputDir;
        public float ClickX = 0.5f;
        public float ClickY = 0.5f;
    }

    /// <summary>
    /// Progress data emitted by the Python backend.
    /// </summary>
    public struct ProgressData
    {
        public float Fraction;
        public string Stage;
        public string Message;
    }

    // -------------------------------------------------------------------------
    // PythonBridge — IPC controller
    // -------------------------------------------------------------------------

    /// <summary>
    /// Manages the IPC bridge between the Unity Editor and the Python backend.
    /// <para>
    /// Architecture:
    /// <list type="bullet">
    ///   <item>Spawns Python as a child process via <see cref="System.Diagnostics.Process"/></item>
    ///   <item>Background threads read stdout/stderr and enqueue messages into a
    ///         <see cref="ConcurrentQueue{T}"/></item>
    ///   <item><see cref="EditorApplication.update"/> drains the queue on the main
    ///         Unity thread (thread-safe, non-blocking)</item>
    ///   <item><see cref="EditorApplication.quitting"/> kills orphaned processes</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PythonBridge
    {
        // ---- Events (fired on the main thread) ----

        /// <summary>Raised when the Python backend reports progress.</summary>
        public static event Action<ProgressData> OnProgress;

        /// <summary>Raised for every general log line from the backend.</summary>
        public static event Action<string> OnLogMessage;

        /// <summary>Raised when the pipeline completes. Payload is the raw JSON string.</summary>
        public static event Action<string> OnCompleted;

        /// <summary>Raised when a fatal error is reported.</summary>
        public static event Action<string> OnError;

        /// <summary>Raised when a preview image (base64 PNG) is available.</summary>
        public static event Action<string> OnPreview;

        // ---- Public state ----

        /// <summary><c>true</c> while a pipeline process is alive.</summary>
        public static bool IsRunning { get; private set; }

        // ---- Private state ----

        private static Process _process;
        private static readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private static bool _callbacksRegistered;

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Launch the pipeline with the given configuration.
        /// </summary>
        public static void StartPipeline(PipelineConfig config)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[PythonBridge] Pipeline is already running.");
                return;
            }

            // --- Locate Python ---
            string pythonExe = FindPython();
            if (string.IsNullOrEmpty(pythonExe))
            {
                RaiseError("Python 3 not found on PATH. Install Python 3.10+ and " +
                           "make sure 'python', 'python3', or 'py' is available.");
                return;
            }

            // --- Locate main.py ---
            string scriptPath = GetMainScriptPath();
            if (!File.Exists(scriptPath))
            {
                RaiseError($"Python entry point not found: {scriptPath}");
                return;
            }

            // --- Ensure output dir ---
            if (!Directory.Exists(config.OutputDir))
                Directory.CreateDirectory(config.OutputDir);

            // --- Build command ---
            string arguments =
                $"\"{scriptPath}\" " +
                $"--video_path \"{config.VideoPath}\" " +
                $"--output_dir \"{config.OutputDir}\" " +
                $"--click_x {config.ClickX.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                $"--click_y {config.ClickY.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            var startInfo = new ProcessStartInfo
            {
                FileName               = pythonExe,
                Arguments              = arguments,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                WorkingDirectory       = GetPythonBackendPath(),
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding  = System.Text.Encoding.UTF8,
            };

            // --- Start process ---
            try
            {
                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _process.OutputDataReceived += OnStdout;
                _process.ErrorDataReceived  += OnStderr;
                _process.Exited             += OnProcessExited;

                IsRunning = true;
                EnsureCallbacksRegistered();

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                Enqueue($"LOG:[PythonBridge] Started (PID {_process.Id})");
                Debug.Log($"[PythonBridge] Started Python process (PID {_process.Id})\n  {pythonExe} {arguments}");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                RaiseError($"Failed to start Python process: {ex.Message}");
                Debug.LogError($"[PythonBridge] Start failed:\n{ex}");
            }
        }

        /// <summary>
        /// Kill the running pipeline process.
        /// </summary>
        public static void Cancel()
        {
            if (!IsRunning || _process == null) return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    Debug.Log("[PythonBridge] Pipeline process killed by user.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PythonBridge] Error killing process: {ex.Message}");
            }
            finally
            {
                CleanupProcess();
            }
        }

        // =====================================================================
        // Python discovery
        // =====================================================================

        /// <summary>
        /// Probe PATH for a Python 3 interpreter.
        /// Candidates tried in order: <c>python</c>, <c>python3</c>, <c>py</c>.
        /// </summary>
        private static string FindPython()
        {
            string[] candidates = { "python", "python3", "py" };

            foreach (string candidate in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName               = candidate,
                        Arguments              = "--version",
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true,
                    };

                    using (Process probe = Process.Start(psi))
                    {
                        string output = probe.StandardOutput.ReadToEnd()
                                      + probe.StandardError.ReadToEnd();
                        probe.WaitForExit(5000);

                        if (probe.ExitCode == 0 && output.Contains("Python 3"))
                        {
                            Debug.Log($"[PythonBridge] Found: {candidate} → {output.Trim()}");
                            return candidate;
                        }
                    }
                }
                catch
                {
                    // Not found — try next candidate
                }
            }

            return null;
        }

        // =====================================================================
        // Path helpers
        // =====================================================================

        private static string GetProjectRoot()
        {
            // Application.dataPath → …/Assets
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string GetPythonBackendPath()
        {
            return Path.Combine(GetProjectRoot(), "PythonBackend~");
        }

        private static string GetMainScriptPath()
        {
            return Path.Combine(GetPythonBackendPath(), "main.py");
        }

        // =====================================================================
        // Editor callbacks
        // =====================================================================

        private static void EnsureCallbacksRegistered()
        {
            if (_callbacksRegistered) return;

            EditorApplication.update   += DrainMessageQueue;
            EditorApplication.quitting += OnEditorQuitting;
            _callbacksRegistered = true;
        }

        /// <summary>
        /// Drain the <see cref="ConcurrentQueue{T}"/> on the main Unity thread.
        /// Called every editor tick.  Caps per-frame work to avoid stalling.
        /// </summary>
        private static void DrainMessageQueue()
        {
            const int maxPerFrame = 50;
            int count = 0;

            while (count < maxPerFrame && _messageQueue.TryDequeue(out string msg))
            {
                DispatchMessage(msg);
                count++;
            }
        }

        // =====================================================================
        // Message parsing / dispatch
        // =====================================================================

        private static void Enqueue(string msg) => _messageQueue.Enqueue(msg);

        /// <summary>
        /// Parse a structured message and fire the appropriate event.
        /// </summary>
        private static void DispatchMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (message.StartsWith("PROGRESS:"))
                ParseProgress(message);
            else if (message.StartsWith("RESULT:"))
                OnCompleted?.Invoke(message.Substring("RESULT:".Length));
            else if (message.StartsWith("ERROR:"))
                OnError?.Invoke(message.Substring("ERROR:".Length));
            else if (message.StartsWith("LOG:"))
                OnLogMessage?.Invoke(message.Substring("LOG:".Length));
            else if (message.StartsWith("PREVIEW:"))
                OnPreview?.Invoke(message.Substring("PREVIEW:".Length));
            else
                OnLogMessage?.Invoke(message); // unstructured → treat as log
        }

        /// <summary>
        /// Parse <c>PROGRESS:&lt;float&gt;:&lt;stage&gt;:&lt;message&gt;</c>.
        /// </summary>
        private static void ParseProgress(string raw)
        {
            try
            {
                string payload    = raw.Substring("PROGRESS:".Length);
                int firstColon    = payload.IndexOf(':');
                if (firstColon < 0) return;

                string fractionStr = payload.Substring(0, firstColon);
                string rest        = payload.Substring(firstColon + 1);

                int secondColon = rest.IndexOf(':');
                if (secondColon < 0) return;

                string stage = rest.Substring(0, secondColon);
                string msg   = rest.Substring(secondColon + 1);

                if (float.TryParse(fractionStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float fraction))
                {
                    OnProgress?.Invoke(new ProgressData
                    {
                        Fraction = fraction,
                        Stage    = stage,
                        Message  = msg,
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PythonBridge] Bad PROGRESS line: {raw} ({ex.Message})");
            }
        }

        // =====================================================================
        // Process event handlers (background threads → enqueue)
        // =====================================================================

        private static void OnStdout(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Enqueue(e.Data);
        }

        private static void OnStderr(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Enqueue("LOG:[stderr] " + e.Data);
        }

        private static void OnProcessExited(object sender, EventArgs e)
        {
            int exitCode = -1;
            try { exitCode = _process?.ExitCode ?? -1; } catch { /* ignore */ }

            if (exitCode != 0)
                Enqueue($"ERROR:Python process exited with code {exitCode}");

            Enqueue($"LOG:[PythonBridge] Process exited (code {exitCode})");

            // Schedule cleanup on main thread
            EditorApplication.delayCall += CleanupProcess;
        }

        private static void OnEditorQuitting()
        {
            Cancel(); // kill orphaned processes
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        private static void CleanupProcess()
        {
            IsRunning = false;

            if (_process != null)
            {
                _process.OutputDataReceived -= OnStdout;
                _process.ErrorDataReceived  -= OnStderr;
                _process.Exited             -= OnProcessExited;

                try { _process.Dispose(); } catch { /* ignore */ }
                _process = null;
            }
        }

        private static void RaiseError(string msg)
        {
            OnError?.Invoke(msg);
        }
    }
}
