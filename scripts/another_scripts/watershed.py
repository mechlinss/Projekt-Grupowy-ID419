import cv2
import numpy as np
import sys
import json


def imshow(img, ax=None):
    try:
        from IPython.display import display, Image
        from matplotlib import pyplot as plt
        if ax is None:
            ret, encoded = cv2.imencode(".jpg", img)
            display(Image(encoded))
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


def find_crystals(photo_path: str, thresh=150, kernel_size=3, open_iter=2, dist_thresh_pct=10):
    img_object = _load_as_bgr(photo_path)
    if img_object is None:
        return None, 0

    gray = cv2.cvtColor(img_object, cv2.COLOR_BGR2GRAY)
    ret, bin_img = cv2.threshold(gray, thresh, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_size, kernel_size))
    bin_img  = cv2.morphologyEx(bin_img, cv2.MORPH_OPEN, kernel, iterations=open_iter)
    sure_bg  = cv2.dilate(bin_img, kernel, iterations=3)

    dist     = cv2.distanceTransform(bin_img, cv2.DIST_L2, 5)
    dist_threshold = (dist_thresh_pct / 100.0) * dist.max()
    _, sure_fg = cv2.threshold(dist, dist_threshold, 255, 0)
    sure_fg  = sure_fg.astype(np.uint8)
    unknown  = cv2.subtract(sure_bg, sure_fg)

    ret, markers = cv2.connectedComponents(sure_fg)
    markers += 1
    markers[unknown == 255] = 0
    markers = cv2.watershed(img_object, markers)

    labels = np.unique(markers)
    coins  = []
    for label in labels[2:]:
        target   = np.where(markers == label, 255, 0).astype(np.uint8)
        contours, _ = cv2.findContours(target, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        coins.append(contours[0])

    result = img_object.copy()
    cv2.drawContours(result, coins, -1, color=(0, 255, 0), thickness=2)

    for i, c in enumerate(coins, start=1):
        M = cv2.moments(c)
        if M["m00"] > 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(result, str(i), (cX, cY),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 0, 255), 2, cv2.LINE_AA)

    return result, len(coins)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"ERROR": "Usage: watershed.py <input_path> <output_path> [thresh] [kernel_size] [open_iter] [dist_thresh_pct]"}))
        sys.exit(1)

    input_path      = sys.argv[1]
    output_path     = sys.argv[2]
    thresh          = int(sys.argv[3]) if len(sys.argv) > 3 else 150
    kernel_size     = int(sys.argv[4]) if len(sys.argv) > 4 else 3
    open_iter       = int(sys.argv[5]) if len(sys.argv) > 5 else 2
    dist_thresh_pct = int(sys.argv[6]) if len(sys.argv) > 6 else 10

    if kernel_size % 2 == 0:
        kernel_size += 1

    result_img, count = find_crystals(input_path, thresh, kernel_size, open_iter, dist_thresh_pct)

    if result_img is None:
        print(json.dumps({"ERROR": f"Cannot load file {input_path}"}))
        sys.exit(1)

    cv2.imwrite(output_path, result_img)
