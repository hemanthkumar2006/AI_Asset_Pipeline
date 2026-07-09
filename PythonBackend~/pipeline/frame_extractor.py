"""
Frame Extractor Module

Decodes a video file with OpenCV and pulls out a fixed number of evenly
spaced frames as JPEGs. This is deliberately simple (uniform sampling, no
scene-change / sharpness scoring yet) — keyframe quality filtering is a
later refinement once segmentation/tracking is in place and we can tell
which frames actually matter.
"""

import os

import cv2


def get_video_metadata(video_path: str) -> dict:
    """
    Read basic metadata from a video file without decoding any frames.

    Args:
        video_path: Path to the input video file

    Returns:
        Dict with fps, frame_count, width, height, duration_sec

    Raises:
        FileNotFoundError: video_path does not exist
        RuntimeError: video_path exists but OpenCV could not open/read it
    """
    if not os.path.exists(video_path):
        raise FileNotFoundError(f"Video file not found: {video_path}")

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise RuntimeError(f"Could not open video (unsupported/corrupt file?): {video_path}")

    try:
        fps = cap.get(cv2.CAP_PROP_FPS) or 0.0
        frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
        width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH) or 0)
        height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT) or 0)

        if frame_count <= 0 or fps <= 0:
            raise RuntimeError(f"Video has no readable frames/fps: {video_path}")

        return {
            "fps": fps,
            "frame_count": frame_count,
            "width": width,
            "height": height,
            "duration_sec": frame_count / fps,
        }
    finally:
        cap.release()


def extract_frames(video_path: str, output_dir: str, max_frames: int = 10,
                    progress_callback=None) -> list:
    """
    Extract up to `max_frames` evenly spaced frames from a video file.

    Args:
        video_path: Path to the input video file (.mp4, .avi, .mov, .mkv)
        output_dir: Directory to save extracted frame images (created if missing)
        max_frames: Maximum number of frames to extract
        progress_callback: Optional callable(done: int, total: int) invoked
            after each frame is saved, for progress reporting

    Returns:
        List of absolute paths to extracted frame images, in video order

    Raises:
        FileNotFoundError: video_path does not exist
        RuntimeError: video could not be opened or has no frames
        ValueError: max_frames is not a positive integer
    """
    if max_frames <= 0:
        raise ValueError("max_frames must be a positive integer")

    metadata = get_video_metadata(video_path)
    total_frames = metadata["frame_count"]

    frames_dir = os.path.join(output_dir, "frames")
    os.makedirs(frames_dir, exist_ok=True)

    frame_count = min(max_frames, total_frames)
    # Evenly spaced indices across the full frame range (inclusive of first/last).
    if frame_count == 1:
        indices = [0]
    else:
        step = (total_frames - 1) / (frame_count - 1)
        indices = [round(i * step) for i in range(frame_count)]

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise RuntimeError(f"Could not open video (unsupported/corrupt file?): {video_path}")

    saved_paths = []
    try:
        for i, frame_index in enumerate(indices):
            cap.set(cv2.CAP_PROP_POS_FRAMES, frame_index)
            ok, frame = cap.read()
            if not ok:
                continue

            frame_path = os.path.join(frames_dir, f"frame_{i:04d}.jpg")
            cv2.imwrite(frame_path, frame)
            saved_paths.append(os.path.abspath(frame_path))

            if progress_callback is not None:
                progress_callback(i + 1, frame_count)
    finally:
        cap.release()

    if not saved_paths:
        raise RuntimeError(f"No frames could be decoded from video: {video_path}")

    return saved_paths
