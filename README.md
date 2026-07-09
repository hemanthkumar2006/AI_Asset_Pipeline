# AI-Driven 3D Asset Pipeline (Unity Editor Extension)
An intuitive, native AI-driven pipeline within the Unity Editor to reconstruct optimized 3D assets from standard video files (MP4, AVI, MOV) using cloud-based AI inference.

---

## Project Overview
This project builds a seamless integration between a Unity C# Editor window and a Python AI backend. It allows developers to feed a video of an object, automatically segment the target object using segment-anything (SAM 2), reconstruct it into a 3D model using TripoSR/SF3D, optimize the topology/UVs, and auto-import the finished textured asset directly into the active scene.

---

## Progress Roadmap

### Week 1: Environment Setup & IPC Pipeline (Current)
*   **Establish communication bridge**: Implemented real-time stdout message streaming using `System.Diagnostics.Process` in Unity C#.
*   **Thread-Safe UI logs**: Implemented a background thread queue reader to process output events cleanly without freezing Unity.
*   **Aesthetic UI**: Custom Unity Editor Window under `Tools > AI 3D Asset Pipeline` featuring progress tracking and color-coded logging.
*   **Asset Importer**: Automatic asset loading, scene placement (centered on camera), selection highlighting, and integration with Unity's Undo stack.
*   **Mock Verification**: Created a dummy Python pipeline generating a textured cube to prove the full round-trip logic works perfectly.

For the full detailed breakdown, code explanation, and diagrams, read the [Week 1 Technical Report](week_1_report.md).

---

## File Structure
```
AI_Asset_Pipeline/
├── Assets/
│   ├── Editor/
│   │   ├── PythonBridge.cs                 # Subprocess & queue manager
│   │   ├── AssetPipelineWindow.cs          # UI Window
│   │   └── AssetImporter.cs                # Asset database compiler & scene spawner
│   └── GeneratedAssets/                    # Target output folder for imported meshes
└── PythonBackend~/                         # Python scripts (ignored by Unity compiler)
    ├── main.py                             # Backend CLI entry point
    ├── requirements.txt                    # Backend dependencies
    └── pipeline/
        ├── ipc.py                          # Stdout message protocol utilities
        └── frame_extractor.py              # Frame processing module (stub)
```

---

## Installation & Setup

### Requirements
*   **Unity Editor**: 2022.3 LTS
*   **Python**: 3.10+ (make sure `python`, `python3`, or `py` is registered in your PATH)

### Quick Start
1. Clone this repository into your Unity project or download the folder.
2. Open the project in Unity 2022.3 LTS.
3. Open the window via the menu: **Tools** > **AI 3D Asset Pipeline**.
4. Browse to any video file.
5. Click **Generate 3D Asset**.
6. Check your active Scene/Hierarchy view; a textured cube will spawn and auto-focus!
