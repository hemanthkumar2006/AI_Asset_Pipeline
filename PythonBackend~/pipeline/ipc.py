"""
IPC Utilities for structured communication between the Python backend and Unity Editor.

Protocol (stdout-based, one message per line):
    PROGRESS:<float>:<stage>:<message>  — Progress update (0.0 to 1.0)
    RESULT:<json>                       — Final result payload
    ERROR:<message>                     — Error message
    LOG:<message>                       — General log message
    PREVIEW:<base64_png>                — Preview image (base64 encoded)

All messages are flushed immediately to ensure real-time streaming.
"""

import sys
import json


def report_progress(fraction: float, stage: str, message: str) -> None:
    """
    Report progress to Unity.

    Args:
        fraction: Progress value between 0.0 and 1.0
        stage: Current pipeline stage name (e.g., "Frame Extraction")
        message: Human-readable status message
    """
    fraction = max(0.0, min(1.0, float(fraction)))
    print(f"PROGRESS:{fraction:.4f}:{stage}:{message}", flush=True)


def report_result(payload: dict) -> None:
    """
    Report the final result as a JSON payload to Unity.

    Args:
        payload: Dictionary containing result data (will be JSON-serialized)
    """
    json_str = json.dumps(payload, separators=(",", ":"))
    print(f"RESULT:{json_str}", flush=True)


def report_error(message: str) -> None:
    """
    Report an error to Unity. This indicates a fatal failure.

    Args:
        message: Error description
    """
    print(f"ERROR:{message}", flush=True)
    sys.stderr.write(f"[PIPELINE ERROR] {message}\n")
    sys.stderr.flush()


def report_log(message: str) -> None:
    """
    Send a general log message to Unity.

    Args:
        message: Log message text
    """
    print(f"LOG:{message}", flush=True)


def report_preview(base64_png: str) -> None:
    """
    Send a preview image to Unity as a base64-encoded PNG.

    Args:
        base64_png: Base64-encoded PNG image data
    """
    print(f"PREVIEW:{base64_png}", flush=True)
