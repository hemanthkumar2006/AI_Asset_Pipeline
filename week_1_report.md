# AI 3D Asset Pipeline - Week 1 Technical Report
**Environment Setup & IPC Pipeline Code Analysis**

This report provides a line-by-line and logic-level breakdown of the code implemented for Week 1. The objective of this phase was to establish a fully functional, thread-safe, and real-time communication channel (Inter-Process Communication / IPC) between the Unity C# Editor and the Python backend.

---

## Part 1: The Integration Architecture

The pipeline uses **subprocess-based stdout streaming** for communication. Instead of opening network ports (which can fail due to firewalls or port conflicts), Unity spawns Python as a child process and captures its stdout (standard output) stream in real-time.

```
+---------------------------+             +---------------------------+
|    Unity Editor Window    |             |    Python Subprocess      |
|  (AssetPipelineWindow.cs) |             |         (main.py)         |
+-------------+-------------+             +-------------+-------------+
              |                                         |
    (Clicks Generate)                                   |
              |                                         |
              v                                         |
+-------------+-------------+                           |
|       PythonBridge        |                           |
|  Spawns process & starts  |-------------------------->| (Simulates stages)
|   background thread       |                           | (Streams outputs)
+-------------+-------------+                           |
              |                                         v
              |                             +-----------+-----------+
  (Pushes lines to Queue)                   |        ipc.py         |
              |                             | Writes formatted lines|
              v                             |  to stdout & flushes  |
+-------------+-------------+               +-----------+-----------+
|  ConcurrentQueue<string>  |                           |
+-------------+-------------+                           |
              |                                         |
      (Drained every                                    |
     Editor update tick)                                |
              |                                         |
              v                                         |
+-------------+-------------+                           |
|     AssetImporter         |                           |
|  (Copies files, refreshes |<--------------------------+ (Writes OBJ/PNG)
|   database & instantiates)|
+---------------------------+
```

---

## Part 2: Python Backend Code Analysis

The Python backend sits in `PythonBackend~/`. The tilde `~` tells Unity to ignore this folder completely, preventing it from generating useless meta files or trying to parse python code.

### 1. IPC Utilities Module
* **File Path**: [ipc.py](file:///c:/Users/heman/Downloads/Final_project/AI_Asset_Pipeline/PythonBackend~/pipeline/ipc.py)
* **Purpose**: Provides clean APIs for the backend code to communicate back to Unity using standard prefixes.

```python
import sys
import json
```
* **`sys`**: Used to flush stdout and print to stderr.
* **`json`**: Used to serialize metadata dictionaries (like file paths) into string payloads that Unity can parse.

#### Progress Reporting Logic
```python
def report_progress(fraction: float, stage: str, message: str) -> None:
    fraction = max(0.0, min(1.0, float(fraction)))
    print(f"PROGRESS:{fraction:.4f}:{stage}:{message}", flush=True)
```
* **`fraction` clamping**: Ensures progress value stays strictly between `0.0` (0%) and `1.0` (100%).
* **Format**: It prints in the format: `PROGRESS:0.2500:Segmentation:Segmenting object...`. 
* **`flush=True`**: Windows buffers print outputs by default. Flushes force Python to write to the stdout stream *immediately*, preventing progress updates from getting stuck in memory and arriving all at once at the end.

#### Result Reporting Logic
```python
def report_result(payload: dict) -> None:
    json_str = json.dumps(payload, separators=(",", ":"))
    print(f"RESULT:{json_str}", flush=True)
```
* **`separators=(",", ":")`**: Eliminates spaces in JSON output to make parsing faster and safer.
* **Format**: Prints `RESULT:{"status":"success",...}` so the C# side knows the pipeline finished successfully and where the files are located.

---

### 2. Main Entry Point
* **File Path**: [main.py](file:///c:/Users/heman/Downloads/Final_project/AI_Asset_Pipeline/PythonBackend~/main.py)
* **Purpose**: Simulates the pipeline execution steps and writes the 3D assets to disk.

#### Windows Encoding Setup
```python
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
```
* **Why this is here**: Windows console shells default to local legacy character maps (like CP1252/latin-1). If Python outputs standard UTF-8 logs (like arrows, ticks, or custom unicode symbols), Python will crash with a `UnicodeEncodeError`. Reconfiguring the streams to `utf-8` with `errors="replace"` guarantees the program will never crash due to console print encodings.

#### Texture Fallback Generation Logic
```python
def generate_dummy_texture(output_dir: str, size: int = 256) -> str:
    texture_path = os.path.join(output_dir, "texture.png")
    try:
        from PIL import Image
        # Checkerboard pattern logic using Pillow
        ...
    except ImportError:
        # Fallback: manually construct a valid PNG file byte-by-byte
```
* **The Challenge**: We want this tool to work *out of the box* without forcing the user to install Python packages first.
* **The Solution**: If `Pillow` (PIL) isn't installed, the script falls back to manually compiling a PNG file byte-by-byte:
  ```python
  width, height = 4, 4
  raw_data = b""
  for y in range(height):
      raw_data += b"\x00"  # PNG Filter byte (0 = None)
      for x in range(width):
          # Alternates colors to draw a checkerboard
          if (x + y) % 2 == 0:
              raw_data += bytes([100, 149, 237]) # Blue
          else:
              raw_data += bytes([70, 130, 180])  # Dark Blue
  ```
* It builds standard PNG structures (`IHDR` chunk, `IDAT` compressed pixel chunk, and `IEND` footer chunk) using Python's standard `zlib` (for compression) and `struct` (for packing integers into big-endian byte sequences) and writes a perfect, valid 4x4 pixel PNG image.

#### 3D Mesh Generation Logic (`generate_dummy_obj`)
This function writes standard Wavefront 3D files:
* **MTL file (`model.mtl`)**: Declares a material named `cube_material` and links it to `texture.png` (`map_Kd texture.png`).
* **OBJ file (`model.obj`)**: Declares 8 vertex coordinates (`v`), 4 texture mapping coordinates (`vt`), 6 surface normals (`vn`), binds the material (`usemtl cube_material`), and defines 6 quad face polygons (`f`) mapping vertices, textures, and normals together.

#### Simulated Pipeline Loop
```python
for i, (name, msg, duration) in enumerate(STAGES):
    base = i / total
    report_progress(base, name, f"Starting {name}...")
    
    steps = 5
    for step in range(steps):
        time.sleep(duration / steps)
        frac = base + ((step + 1) / steps) * (1.0 / total)
        report_progress(frac, name, msg)
```
* Dynamically calculates progress. For instance, if stage 2 of 5 is running, it scales the progress bar strictly inside the `0.20` to `0.40` range so the bar moves smoothly forward instead of jumping.

---

## Part 3: Unity C# Editor Code Analysis

### 1. The Subprocess Controller
* **File Path**: [PythonBridge.cs](file:///c:/Users/heman/Downloads/Final_project/AI_Asset_Pipeline/Assets/Editor/PythonBridge.cs)
* **Purpose**: Coordinates subprocess lifetimes and coordinates thread-safe stdout forwarding.

```csharp
private static readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
```
* **`ConcurrentQueue`**: A thread-safe queue. The background threads that capture stdout/stderr write to this queue, and Unity's main UI thread reads from it. This prevents race conditions.

#### Auto-Finding Python
```csharp
private static string FindPython()
{
    string[] candidates = { "python", "python3", "py" };
    ...
}
```
* Iterates through command-line command aliases. It runs `<candidate> --version` in the background with a 5-second timeout. If it gets exit code 0 and stdout contains `"Python 3"`, it returns that command name. This guarantees it works whether Python is installed as `python`, `python3`, or through the Windows Python Launcher (`py`).

#### Process Execution
```csharp
var startInfo = new ProcessStartInfo
{
    FileName               = pythonExe,
    Arguments              = arguments,
    UseShellExecute        = false, // Required to redirect streams
    RedirectStandardOutput = true,
    RedirectStandardError  = true,
    CreateNoWindow         = true,  // Hide the black CMD window
    WorkingDirectory       = GetPythonBackendPath(),
    StandardOutputEncoding = System.Text.Encoding.UTF8,
    StandardErrorEncoding  = System.Text.Encoding.UTF8,
};
```
* **`UseShellExecute = false`**: Instructs the OS to let C# capture stdout/stderr direct streams.
* **`RedirectStandardOutput/Error`**: Enables custom program logic to read output logs.
* **`StandardOutputEncoding = System.Text.Encoding.UTF8`**: Matches the UTF-8 encoding we forced on the Python side, making sure text doesn't corrupt.

#### Thread Safety and Queue Draining
```csharp
private static void EnsureCallbacksRegistered()
{
    if (_callbacksRegistered) return;
    EditorApplication.update   += DrainMessageQueue;
    EditorApplication.quitting += OnEditorQuitting;
    _callbacksRegistered = true;
}
```
* **`EditorApplication.update`**: A delegate that fires many times per second on Unity's main thread while the editor is open.
* **`DrainMessageQueue()`**:
  ```csharp
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
  ```
  Drains up to 50 logs per frame. Limiting this prevents Unity from stuttering if Python outputs thousands of logs at once.
* **`DispatchMessage()`**: Reads prefixes (`PROGRESS:`, `LOG:`, `RESULT:`, `ERROR:`, `PREVIEW:`) and triggers corresponding C# events (like `OnProgress` and `OnCompleted`).

---

### 2. The Editor Window
* **File Path**: [AssetPipelineWindow.cs](file:///c:/Users/heman/Downloads/Final_project/AI_Asset_Pipeline/Assets/Editor/AssetPipelineWindow.cs)
* **Purpose**: Renders the User Interface panel.

```csharp
[MenuItem("Tools/AI 3D Asset Pipeline")]
public static void ShowWindow()
{
    var win = GetWindow<AssetPipelineWindow>("AI Asset Pipeline");
    win.minSize = new Vector2(460, 520);
}
```
* Registers a menu item in the editor. `minSize` ensures the layout components don't collapse into illegible boxes.

#### Draw UI Layout (`OnGUI`)
* Uses `EditorGUILayout.BeginHorizontal()` and `EndHorizontal()` to draw labels and browse buttons side-by-side.
* Uses `EditorGUI.ProgressBar` to render the graphic indicator block.
* Draws a ScrollView containing the list of parsed log elements:
  ```csharp
  string color = entry.Type switch
  {
      LogType.Error   => "#FF6B6B", // Soft red
      LogType.Warning => "#FFD93D", // Soft yellow
      _               => "#C8C8C8", // Slate white
  };
  ```
  Uses Unity's Rich Text markup (`<color=...></color>`) to draw user-friendly, highly legible color-coded log entries.
* **`Repaint()`**: If the subprocess is actively running, it forces the window to repaint every frame to ensure progress bar animations look fluid.

---

### 3. The Import Pipeline
* **File Path**: [AssetImporter.cs](file:///c:/Users/heman/Downloads/Final_project/AI_Asset_Pipeline/Assets/Editor/AssetImporter.cs)
* **Purpose**: Ingests files into the Unity engine and places them in the scene.

#### JSON Data Transfer Object (DTO)
```csharp
[Serializable]
private class PipelineResult
{
    public string status;
    public string mesh_path;
    public string mtl_path;
    public string texture_path;
    public string output_dir;
}
```
* Configures variables that match the JSON keys returned by Python's `report_result()` payload. `JsonUtility.FromJson` reads this structure to extract file locations.

#### Importing & Scene Setup
```csharp
// 1. Copy generated files into Assets/
string destDir = Path.Combine(Application.dataPath, "GeneratedAssets", stamp);
Directory.CreateDirectory(destDir);
CopyDirectoryContents(outputDir, destDir);

// 2. Alert Unity's Database to look for new assets
AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

// 3. Load the OBJ mesh asset
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(objAssetPath);

// 4. Spawn it inside the editor scene
GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

// 5. Position at the Scene viewport focus center
SceneView sv = SceneView.lastActiveSceneView;
if (sv != null) instance.transform.position = sv.pivot;

// 6. Hook into Undo system
Undo.RegisterCreatedObjectUndo(instance, "Generate 3D Asset");

// 7. Select the object in hierarchy and zoom in Scene camera
Selection.activeGameObject = instance;
if (sv != null) sv.FrameSelected();
```
* **`AssetDatabase.Refresh()`**: Forces Unity to compile/import the raw files, generating their corresponding `.meta` files and assets.
* **`PrefabUtility.InstantiatePrefab()`**: Spawns the model as an active scene asset.
* **`Undo.RegisterCreatedObjectUndo()`**: Ensures the spawn is registered in Unity's action stack, allowing users to safely delete the object by simply hitting `Ctrl+Z`.
* **`sv.FrameSelected()`**: Automatically focuses the scene view camera directly on the spawned model, giving immediate visual feedback.

---

## Part 4: Verification Checklist

| Phase | Test Component | Logic Checked | Result |
|---|---|---|---|
| **Python Side** | `python main.py` | Argument parsing, simulated timing, binary file generation, ASCII output streaming | Passed (0% to 100% updates, OBJ/MTL/PNG generated successfully) |
| **Windows encoding** | UTF-8 stdout redirection | Ensured Unicode symbols do not crash Python execution | Passed (No Unicode crashes) |
| **Unity Side** | `Tools > AI 3D Asset Pipeline` | Process starting, queue tracking, UI logs drawing, files import, object creation, undo stack | Passed (Spawned GameObjects visible in Hierarchy and project folders) |
