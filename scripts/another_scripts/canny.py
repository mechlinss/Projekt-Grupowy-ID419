import cv2
import numpy as np
import sys
import json


def imshow(img, ax=None):
    try:
        from IPython.display import display, Image
        import matplotlib.pyplot as plt
        if ax is None:
            ret, encoded = cv2.imencode(".jpg", img)
            display(Image(encoded.tobytes()))
        else:
            ax.imshow(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
            ax.axis('off')
    except ImportError:
        pass


def _load_as_bgr(path: str):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        return None
    if img.dtype == 'uint16':
        img = (img / 256).astype('uint8')
    elif img.dtype != 'uint8':
        img = cv2.normalize(img, None, 0, 255, cv2.NORM_MINMAX).astype('uint8')
    if len(img.shape) == 2:
        img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)
    return img


def find_crystals(photo_path: str, canny_t1=80, canny_t2=90, kernel_size=3, close_iter=2, min_area=50):
    img = _load_as_bgr(photo_path)
    if img is None:
        return None, 0

    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blur, canny_t1, canny_t2)

    kernel = np.ones((kernel_size, kernel_size), np.uint8)
    edges_closed = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel, iterations=close_iter)

    contours, _ = cv2.findContours(edges_closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    diamonds = [c for c in contours if cv2.contourArea(c) > min_area]
    result = img.copy()
    cv2.drawContours(result, diamonds, -1, (0, 255, 0), 2)

    for i, c in enumerate(diamonds, start=1):
        M = cv2.moments(c)
        if M["m00"] > 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(result, str(i), (cX, cY),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 0, 255), 2, cv2.LINE_AA)

    return result, len(diamonds)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"ERROR": "Usage: canny.py <input_path> <output_path> [canny_t1] [canny_t2] [kernel_size] [close_iter] [min_area]"}))
        sys.exit(1)

    input_path  = sys.argv[1]
    output_path = sys.argv[2]
    canny_t1    = int(sys.argv[3]) if len(sys.argv) > 3 else 80
    canny_t2    = int(sys.argv[4]) if len(sys.argv) > 4 else 90
    kernel_size = int(sys.argv[5]) if len(sys.argv) > 5 else 3
    close_iter  = int(sys.argv[6]) if len(sys.argv) > 6 else 2
    min_area    = int(sys.argv[7]) if len(sys.argv) > 7 else 50

    if kernel_size % 2 == 0:
        kernel_size += 1

    result_img, count = find_crystals(input_path, canny_t1, canny_t2, kernel_size, close_iter, min_area)

    if result_img is None:
        print(json.dumps({"ERROR": f"Cannot load file {input_path}"}))
        sys.exit(1)

    cv2.imwrite(output_path, result_img)
