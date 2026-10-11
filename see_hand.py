import socket
import time
from pathlib import Path
import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
TARGET = ("127.0.0.1", 5005)

# 21 个关键点里，哪些点连成骨头
BONES = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (0, 9), (9, 10), (10, 11), (11, 12),
    (0, 13), (13, 14), (14, 15), (15, 16),
    (0, 17), (17, 18), (18, 19), (19, 20),
    (5, 9), (9, 13), (13, 17),
]


def main():
    options = vision.HandLandmarkerOptions(
        base_options=python.BaseOptions(
            model_asset_path=str(Path(__file__).with_name("hand_landmarker.task"))
        ),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=2,
    )
    landmarker = vision.HandLandmarker.create_from_options(options)

    cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)
    if not cap.isOpened():
        raise SystemExit("打不开摄像头")

    t0 = time.time()
    while True:
        ok, frame = cap.read()
        if not ok:
            break

        frame = cv2.flip(frame, 1)  # 镜像，手往左动，画面也往左
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        timestamp_ms = int((time.time() - t0) * 1000)
        result = landmarker.detect_for_video(mp_image, timestamp_ms)

        h, w = frame.shape[:2]
        if result.hand_landmarks:
            for hand_i, pts in enumerate(result.hand_landmarks):
                xy = [(int(p.x * w), int(p.y * h)) for p in pts]
                color = (80, 220, 120) if hand_i == 0 else (255, 160, 40)

                for a, b in BONES:
                    cv2.line(frame, xy[a], xy[b], color, 2)
                for x, y in xy:
                    cv2.circle(frame, (x, y), 4, color, -1)

                thumb, index = xy[4], xy[8]
                cv2.line(frame, thumb, index, color, 2)
                pinch = ((thumb[0] - index[0]) ** 2 + (thumb[1] - index[1]) ** 2) ** 0.5
                if hand_i == 0:
                    palm = ((pts[5].x - pts[17].x) ** 2 + (pts[5].y - pts[17].y) ** 2) ** 0.5
                    pinch_norm = 0.0 if palm < 1e-6 else (
                        ((pts[4].x - pts[8].x) ** 2 + (pts[4].y - pts[8].y) ** 2) ** 0.5 / palm
                    )
                    message = f"{pts[8].x:.4f},{1 - pts[8].y:.4f},{pinch_norm:.4f}"
                    sock.sendto(message.encode("ascii"), TARGET)

                cv2.circle(frame, index, 8, (0, 0, 255), 2)
                cv2.putText(
                    frame,
                    f"hand{hand_i} pinch {pinch:.0f}px  index ({index[0]}, {index[1]})",
                    (20, 40 + hand_i * 40),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.7,
                    color,
                    2,
                )
        else:
            cv2.putText(
                frame,
                "no hand",
                (20, 40),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.9,
                (0, 0, 255),
                2,
            )

        cv2.imshow("see hand", frame)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()