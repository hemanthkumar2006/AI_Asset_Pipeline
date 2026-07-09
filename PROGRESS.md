# Progress Log

Running devlog of what actually got built, in what order, and why. Complements
`timed_daily_plan.md` (the schedule) and `CLAUDE.md` (the static project
charter) — this file is the record of real work, kept mainly so I can explain
decisions accurately in interviews later.

Each entry: what was built, why it was built that way, and anything that
diverged from the plan.

---

## Week 1 — Environment Setup & IPC Pipeline

### Thu Jul 9, 2026

**Built:**
- Unity Editor skeleton: `Tools > AI 3D Asset Pipeline` window with video file
  browsing, a progress bar, and a color-coded scrolling log panel
  (`AssetPipelineWindow.cs`).
- `PythonBridge.cs` — spawns the Python backend as a subprocess, redirects
  stdout/stderr, and forwards lines to the main thread via a
  `ConcurrentQueue<string>` drained on `EditorApplication.update`. Avoids
  network ports entirely (no firewall/port-conflict risk).
- `AssetImporter.cs` — on pipeline completion, copies generated files into
  `Assets/GeneratedAssets/<timestamp>/`, triggers `AssetDatabase.Refresh`,
  instantiates the mesh in the open scene at the Scene View pivot, and wires
  it into Unity's Undo stack.
- Python backend (`main.py`, `pipeline/ipc.py`) — a line-based stdout
  protocol (`PROGRESS:`, `LOG:`, `RESULT:`, `ERROR:`, `PREVIEW:`) plus a mock
  pipeline that simulates five stages and writes a placeholder textured cube
  (OBJ + MTL + PNG) to prove the full Unity ⇄ Python round trip works.
- **Real frame extraction** (`pipeline/frame_extractor.py`): OpenCV-based
  `get_video_metadata()` and `extract_frames()` — decodes the video, samples
  up to 10 evenly-spaced frames, saves them as JPEGs. Wired into `main.py`'s
  "Frame Extraction" stage, replacing the `time.sleep` simulation for that
  stage only (Segmentation/3D Reconstruction/Mesh Processing/Export are still
  simulated — those land in later weeks). Verified against a synthetic
  90-frame test video: correctly extracted 10 distinct frames; a missing
  video file now fails fast with a proper `ERROR:` message instead of
  silently proceeding.
- Removed stale `README.md` / `week_1_report.md` (superseded by `CLAUDE.md` +
  `timed_daily_plan.md`, and described an older TripoSR/SF3D-based design
  that no longer matches the current multi-view Tripo AI/Meshy plan).

**Why built this way:**
- Subprocess + stdout streaming over a local network server: simpler, no
  port/firewall handling, and sufficient since Unity and Python always run on
  the same machine.
- Proved the IPC round trip with a *mock* pipeline first, before any real AI
  model integration — isolates plumbing bugs from model/API bugs.
- Frame extraction uses uniform sampling, not scene-change/sharpness
  scoring — the plan explicitly defers keyframe-quality filtering until
  segmentation/tracking exist and can inform which frames actually matter.

**Diverged from `timed_daily_plan.md`:**
- Plan's Day 1 line item was "video import + frame extraction script." What
  actually got built first was the full IPC bridge (Python subprocess ↔
  Unity UI ↔ asset import) plus the mock pipeline, with real frame
  extraction added after. Video *import* on the Unity side is still just a
  file-path browser — scrubbing/previewing a frame as a texture is Day 2's
  task and hasn't started.

**Open for next session:** frame preview UI (scrub video, display a frame as
a texture in the editor window) — Day 2 per the plan.
