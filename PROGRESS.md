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

---

### Thu Jul 9, 2026 (session 2) — rest of Week 1's project track

Compressed the remaining Fri–Wed project tasks (frame preview, click-to-select,
SAM2 request, mask overlay) into one session, at the user's request to
"complete Week 1 entirely." DSA tasks were explicitly excluded — those are
personal interview-prep practice and solving them here would defeat the
purpose.

**Built:**
- `pipeline/frame_extractor.py`: added `extract_single_frame(video_path,
  frame_time)` — seeks by timestamp (`CAP_PROP_POS_MSEC`) and decodes one
  frame, for on-demand preview rather than the bulk `extract_frames()` path.
- `pipeline/imaging.py` (new): `encode_bgr_to_base64_png()` — turns an OpenCV
  frame into the base64 string the existing `PREVIEW:` IPC message already
  expected (the protocol supported this from Week 1 session 1; nothing sent
  it until now).
- `pipeline/segmentation.py` (new): `segment_object(frame, click_x, click_y)`.
  Real path (`_segment_via_replicate`) is a stub — needs a live Replicate
  account/API key to confirm the SAM2 model version and request/response
  schema (still blocked on the user creating that account — Sat's task).
  Until `REPLICATE_API_TOKEN` is set, falls back to a local OpenCV
  flood-fill mock from the click point, so the whole click → mask → overlay
  flow is testable today without any API cost.
- `pipeline/overlay.py` (new): `make_overlay()` — tints the masked region and
  draws a contour outline, for display back in Unity.
- `main.py`: added `--mode {pipeline,metadata,preview,segment}`. `pipeline`
  is unchanged (default). `metadata` reports fps/dimensions/duration.
  `preview` sends back one decoded frame as a `PREVIEW:` message. `segment`
  runs `segment_object` + `make_overlay` and sends back the mask overlay.
  `--output_dir` is now optional (only required for `pipeline` mode).
- `PythonBridge.cs`: refactored `StartPipeline`'s process-launch logic into a
  shared private `Run(string arguments)`, and added `RequestMetadata()`,
  `RequestPreviewFrame()`, `RequestSegmentation()` — all one-shot subprocess
  calls reusing the same event system (`OnPreview` already existed from
  session 1 but had no producer or consumer until now).
- `AssetPipelineWindow.cs`: new "Frame Preview & Click-to-Select" section —
  duration-bounded time slider + "Load Frame" button, the returned frame
  drawn via `GUI.DrawTexture`, raw `Event.current` mouse-down detection
  inside that rect to capture a normalized click point (drawn as a red
  crosshair), and a "Segment At Click" button that requests segmentation and
  swaps the displayed texture for the mask overlay. Added a
  `RequestMode` enum (`_lastRequestMode`) so the shared `OnCompleted` event
  can be parsed differently depending on which one-shot request is in
  flight (metadata JSON vs. segment-result JSON vs. the real pipeline
  result) instead of always calling `AssetImporter.ImportGeneratedAssets`.

**Why built this way:**
- Considered Unity's `VideoPlayer` for scrubbing instead of round-tripping
  through Python per frame, but the project already treats Python/OpenCV as
  the single source of truth for video decoding (frame extraction is there
  too) — adding a second, Unity-side video decode path would mean keeping
  two decoders' frame-indexing in sync for no real benefit at this scale.
  "Scrub" here means: move a slider, click "Load Frame" — not a live drag
  preview — because a live drag would spawn a new Python subprocess per
  mouse-move event, which is wasteful and would visibly stutter.
- Mock segmentation uses OpenCV flood-fill (color-similarity region growing
  from the click point), not a random/fake mask — it's a legitimate simple
  CV technique, cheap to run, and produces a real mask so the overlay
  rendering and IPC round-trip get genuine test coverage before SAM2 is
  wired in.
- `segment_object()` returns `used_real_api` explicitly so the UI (and this
  log) can always tell whether a given result came from the real model or
  the placeholder — avoids silently presenting mock output as if it were
  real segmentation.

**Not done / explicitly blocked:**
- **Sat's task (get a Replicate/SAM2 API key, test one manual call)** needs
  a real account signup — that's the user's step, not something doable from
  here. `_segment_via_replicate()` in `pipeline/segmentation.py` is the
  landing spot: once `REPLICATE_API_TOKEN` is set and the model
  version/schema are confirmed, filling in that one function is the only
  change needed — `main.py` and the Unity side don't change.
- Click-marker → GUI coordinate mapping (`HandlePreviewClick` in
  `AssetPipelineWindow.cs`) is reasoned through against Unity's documented
  `GUI.DrawTexture`/`Texture2D.LoadImage` orientation behavior but not
  visually confirmed in the actual Editor (no way to drive the Unity GUI
  from this environment) — worth a quick sanity check the first time this
  window is opened: click near the top of the frame and confirm the overlay
  contour lands in the right place.
- Verified everything backend-side against a synthetic OpenCV-generated test
  video (metadata, preview, segment, and full pipeline modes all pass); did
  not verify against a real-world video file.
