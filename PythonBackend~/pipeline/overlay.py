"""Blend a binary mask onto a frame for preview/debug visualization."""

import cv2
import numpy as np


def make_overlay(frame_bgr: np.ndarray, mask: np.ndarray,
                  color: tuple = (60, 180, 255), alpha: float = 0.45) -> np.ndarray:
    """
    Tint the masked region of a frame and outline it, for display in Unity.

    Args:
        frame_bgr: original frame, BGR numpy array
        mask: uint8 (H, W) array, 255 = foreground
        color: BGR tint color
        alpha: blend strength over the masked region (0.0-1.0)

    Returns:
        BGR numpy array, same size as frame_bgr
    """
    overlay = frame_bgr.copy()
    mask_bool = mask > 0

    if np.any(mask_bool):
        tint = np.full_like(frame_bgr, color, dtype=np.uint8)
        blended = cv2.addWeighted(frame_bgr, 1.0 - alpha, tint, alpha, 0.0)
        overlay[mask_bool] = blended[mask_bool]

        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        cv2.drawContours(overlay, contours, -1, color, 2)

    return overlay
