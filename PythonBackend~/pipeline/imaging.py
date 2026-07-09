"""Image encoding helpers for sending frames back to Unity over stdout."""

import base64

import cv2
import numpy as np


def encode_bgr_to_base64_png(frame_bgr: np.ndarray) -> str:
    """
    Encode a BGR numpy array (OpenCV's native format) as a base64 PNG string,
    suitable for `pipeline.ipc.report_preview`.
    """
    ok, buf = cv2.imencode(".png", frame_bgr)
    if not ok:
        raise RuntimeError("Failed to encode frame as PNG")
    return base64.b64encode(buf.tobytes()).decode("ascii")
