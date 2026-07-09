"""
Frame Extractor Module (Stub)

Will be implemented in Week 3 with:
- OpenCV video decoding
- Keyframe detection via scene-change thresholds
- Laplacian sharpness scoring for quality filtering
- Configurable max frame count and sampling strategy
"""


def extract_frames(video_path: str, output_dir: str, max_frames: int = 10) -> list:
    """
    Extract high-quality keyframes from a video file.

    Args:
        video_path: Path to the input video file (.mp4, .avi, .mov)
        output_dir: Directory to save extracted frame images
        max_frames: Maximum number of frames to extract

    Returns:
        List of paths to extracted frame images

    Raises:
        NotImplementedError: This is a stub for Week 1
    """
    raise NotImplementedError(
        "Frame extraction will be implemented in Week 3 "
        "with OpenCV keyframe detection and Laplacian sharpness scoring."
    )
