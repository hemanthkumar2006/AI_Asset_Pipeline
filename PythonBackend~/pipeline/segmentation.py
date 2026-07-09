"""
Object segmentation from a single click point.

The real path calls SAM2 via Replicate (see `_segment_via_replicate`) — not
yet wired up end to end since it needs a live Replicate account and API key
(timed_daily_plan.md Week 1 Sat task). Until `REPLICATE_API_TOKEN` is set,
`segment_object` falls back to a local OpenCV flood-fill mock so the
click -> mask -> overlay flow is fully testable without a network call or
API cost. Swapping in the real call later means filling in
`_segment_via_replicate` — the calling code (`main.py`) doesn't change.
"""

import os

import cv2
import numpy as np


def segment_object(frame_bgr: np.ndarray, click_x: float, click_y: float) -> tuple:
    """
    Segment the object at a clicked point in a frame.

    Args:
        frame_bgr: input frame, BGR numpy array
        click_x, click_y: normalized click coordinates (0.0-1.0, origin top-left)

    Returns:
        (mask, used_real_api) — mask is a uint8 (H, W) array, 255 = foreground,
        0 = background. used_real_api is False whenever the local mock ran.
    """
    height, width = frame_bgr.shape[:2]
    px = int(np.clip(click_x, 0.0, 1.0) * (width - 1))
    py = int(np.clip(click_y, 0.0, 1.0) * (height - 1))

    api_token = os.environ.get("REPLICATE_API_TOKEN")
    if api_token:
        mask = _segment_via_replicate(frame_bgr, px, py, api_token)
        if mask is not None:
            return mask, True
        # Real call unavailable/failed — fall through to the mock rather
        # than hard-failing the whole segment request.

    return _segment_via_floodfill(frame_bgr, px, py), False


def _segment_via_replicate(frame_bgr: np.ndarray, px: int, py: int, api_token: str):
    """
    Real SAM2 call via Replicate. Returns None (never attempted) until a
    live API key has been used to confirm the model version string and
    request/response schema — see timed_daily_plan.md Week 1 Sat task.
    """
    try:
        import replicate  # noqa: F401  (optional dep — only needed for this path)
    except ImportError:
        return None

    # TODO(Week 1 Sat): once REPLICATE_API_TOKEN is confirmed working,
    # call the SAM2 model with (frame_bgr, px, py) as the click prompt and
    # decode its returned mask into a uint8 (H, W) array here.
    return None


def _segment_via_floodfill(frame_bgr: np.ndarray, px: int, py: int) -> np.ndarray:
    """Local mock: flood-fill outward from the click point by color similarity."""
    height, width = frame_bgr.shape[:2]
    flood_mask = np.zeros((height + 2, width + 2), dtype=np.uint8)

    tolerance = 24
    cv2.floodFill(
        frame_bgr.copy(),
        flood_mask,
        (px, py),
        newVal=0,
        loDiff=(tolerance, tolerance, tolerance),
        upDiff=(tolerance, tolerance, tolerance),
        flags=cv2.FLOODFILL_MASK_ONLY | (255 << 8),
    )

    return flood_mask[1:-1, 1:-1]
