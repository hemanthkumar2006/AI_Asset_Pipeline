# CLAUDE.md — Video-to-3D Unity Asset Pipeline

## Project Goal
A Unity tool that turns a raw video into a game-ready 3D asset, end to end:
**Video → user clicks the object they want → object is segmented and tracked through the video → segmented views sent to a 3D generation API → mesh cleaned up → auto-imported into the Unity scene.**

## Why this project exists / what makes it different
This is NOT a competitor to 3D-generation models like Tripo AI or Meshy — it's a workflow layer that sits *in front of* one. Those tools assume you already have a clean, single, well-framed image of your subject. This tool solves the step before that: going from messy real-world video to a correctly-selected, multi-angle, ready-to-generate input — and then finishes the job by importing the result straight into an open Unity scene, with no manual export/import step.

## Key constraint that shaped the architecture
Development machine has **no dedicated NVIDIA GPU**, so nothing that requires local CUDA inference (SAM 2, TripoSR, or any local PyTorch model) can run on-device. This is not a limitation to work around — it's a deliberate architectural choice: all AI inference runs via **cloud APIs**, which also makes the finished tool usable on any machine regardless of hardware.

## Pipeline (current design)
1. **Video import** — user drops a video into the Unity tool.
2. **Frame preview + click-to-select** — user scrubs to a frame and clicks the object they want; click coordinates are captured and mapped to pixel space.
3. **Segmentation + tracking (cloud SAM 2 via API, e.g. Replicate)** — the clicked point segments the object in that frame; SAM 2's video tracking propagates the mask across the rest of the extracted frames.
   - If tracking confidence drops for several consecutive frames, pause and ask the user to re-click on that frame rather than silently degrading.
4. **Multi-view frame selection** — automatically pick 2–4 well-spaced frames (different angles) of the tracked object, rather than using only a single frame. This is the key reason a video input is actually useful over a single photo — don't waste it by only sending one frame to the generation API.
5. **3D generation (cloud API)** — send the multi-view crops to a **multi-image-capable** 3D generation API (Tripo AI or Meshy preferred over TripoSR, since TripoSR only accepts a single image and would throw away the multi-view data). Prefer GLB output over separate OBJ+texture — cleaner for Unity's glTF import path.
6. **Local mesh cleanup** — decimation, basic cleanup, optional LOD tiers. This step runs locally on CPU (no GPU needed).
7. **Unity import** — resulting GLB auto-imports into the open Unity scene with correct scale/orientation and material applied.

## Design decisions worth remembering (and being able to explain in interviews)
- **Cloud API over local inference**: not a workaround, a real architecture choice — device-independent, no GPU requirement for end users.
- **Multi-view over single-frame**: video gives multiple angles for free; throwing that away by picking one frame for a single-image model wastes the input's main advantage. Use a multi-image-capable API instead.
- **User-guided selection over full-auto detection**: click-to-select is simpler and more reliable than trying to auto-detect "the" object in an arbitrary video, and gives the user control over ambiguous scenes.
- **Cost awareness**: cloud generation costs real money per call (~$0.01–0.10 range depending on API/quality tier) — cache results keyed by video+object so re-runs on the same object don't re-bill, and offer a cheap "draft" quality tier before spending on a final high-quality generation.

## Engineering practices to maintain
- **Error handling**: cloud calls will time out or fail — retry with backoff, and surface a clear error state in the Unity UI rather than a stuck spinner.
- **Progress UI**: cloud round-trips take 30–90+ seconds — always show stage labels (Extracting → Segmenting → Generating → Importing), never a single generic spinner.
- **Never hardcode API keys** — pull from an env var or an editor-only settings asset that's gitignored.
- **Test coverage**: verify the full pipeline against several different real videos (simple convex object, textured object, partially occluded object) before considering any milestone "done."

## Tech stack
- **Unity** (C#) — UI, video import, scene integration, local mesh cleanup
- **Cloud APIs** — SAM 2 (segmentation/tracking), Tripo AI or Meshy (multi-view 3D generation), likely via Replicate or direct API
- **Local processing** — frame extraction (ffmpeg or Unity VideoPlayer), mesh decimation/cleanup (CPU-only, no GPU dependency)

## Current status / build plan
Full week-by-week build plan with daily timings lives in `timed_daily_plan.md` in this repo (or wherever it's saved). What actually got built each day (vs. planned) is logged in `PROGRESS.md`. Target: MVP fully working and demo-ready by **Sept 2, 2026**. If a milestone is behind schedule, cut scope on nice-to-haves (LOD generation, multi-object selection) before cutting testing/polish time — a reliable simple pipeline beats an ambitious broken one.

## Explicitly out of scope for MVP (backlog, not now)
- Multi-object selection in a single pass
- Local/offline fallback path for machines with a GPU
- Mobile-specific texture/poly budget presets
- LOD tier generation beyond one basic pass

## How to work with me on this project
- I'm a student building this as a resume/portfolio project for game-dev internship applications — prioritize working, demonstrable, explainable code over premature optimization or extra features.
- When making a technical choice, briefly note *why*, since I need to be able to explain design decisions in interviews.
- Flag any step that would meaningfully increase cloud API cost before implementing it.
