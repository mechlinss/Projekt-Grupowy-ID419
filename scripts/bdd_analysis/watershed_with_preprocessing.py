import cv2
import numpy as np
import csv
import os
import sys
import json

# --- DEFAULT PARAMETERS ---
SIGMOID_ALPHA   = 15.0
SIGMOID_BETA    = 0.13
MIN_AREA_PX     = 150
DIST_THRESH_PCT = 20      # percent of dist.max() used as seed threshold
CLAHE_CLIP      = 3.0


def apply_sigmoid_contrast(image, alpha, beta):
    img_float = image / 255.0
    sigmoid   = 1 / (1 + np.exp(-alpha * (img_float - beta)))
    return np.uint8(cv2.normalize(sigmoid, None, 0, 255, cv2.NORM_MINMAX))


def save_debug_image(debug_dir, step_number, name, image, is_float=False):
    filename = f"{step_number:02d}_{name}.png"
    filepath = os.path.join(debug_dir, filename)
    if is_float or image.dtype != np.uint8:
        norm_img = cv2.normalize(image, None, 0, 255, cv2.NORM_MINMAX, dtype=cv2.CV_8U)
        cv2.imwrite(filepath, norm_img)
    else:
        cv2.imwrite(filepath, image)


def process_image(input_path: str, output_path: str,
                  sigmoid_alpha=15, sigmoid_beta_pct=13,
                  min_area=150, dist_thresh_pct=20, clahe_clip=3):
    output_dir = os.path.dirname(output_path) or '.'
    debug_dir  = os.path.join(output_dir, 'debug_steps')
    os.makedirs(output_dir, exist_ok=True)
    os.makedirs(debug_dir,  exist_ok=True)

    sigmoid_beta = sigmoid_beta_pct / 100.0

    img_raw = cv2.imread(input_path, cv2.IMREAD_UNCHANGED)
    if img_raw is None:
        return None, 0, 0.0

    if img_raw.dtype == 'uint16':
        img_raw = (img_raw / 256).astype('uint8')
    elif img_raw.dtype != 'uint8':
        img_raw = cv2.normalize(img_raw, None, 0, 255, cv2.NORM_MINMAX).astype('uint8')
    if len(img_raw.shape) == 2:
        img_raw = cv2.cvtColor(img_raw, cv2.COLOR_GRAY2BGR)

    gray         = cv2.cvtColor(img_raw, cv2.COLOR_BGR2GRAY)
    gray_roi     = gray
    img_roi_color = img_raw.copy()

    save_debug_image(debug_dir, 1, "Original", gray_roi)

    clahe      = cv2.createCLAHE(clipLimit=float(clahe_clip), tileGridSize=(32, 32))
    gray_clahe = clahe.apply(gray_roi)
    save_debug_image(debug_dir, 2, "CLAHE_applied", gray_clahe)

    gray_blur = cv2.medianBlur(gray_clahe, 5)
    save_debug_image(debug_dir, 3, "MedianBlur_applied", gray_blur)

    gray_enhanced = apply_sigmoid_contrast(gray_blur, sigmoid_alpha, sigmoid_beta)
    save_debug_image(debug_dir, 4, "Sigmoid_Contrast", gray_enhanced)

    ret, thresh = cv2.threshold(gray_enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    save_debug_image(debug_dir, 5, "Otsu_Thresh", thresh)

    dist_transform = cv2.distanceTransform(thresh, cv2.DIST_L2, 5)
    save_debug_image(debug_dir, 6, "Distance_Map", dist_transform, is_float=True)

    dist_smooth = cv2.GaussianBlur(dist_transform, (21, 21), 0)
    save_debug_image(debug_dir, 7, "Distance_Map_Blurred", dist_smooth, is_float=True)

    seed_threshold = (dist_thresh_pct / 100.0) * dist_smooth.max()
    _, peaks = cv2.threshold(dist_smooth, seed_threshold, 255, 0)
    peaks = np.uint8(peaks)
    save_debug_image(debug_dir, 8, "Starting_Points", peaks)

    _, markers    = cv2.connectedComponents(peaks)
    kernel_dial   = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    markers       = cv2.dilate(markers.astype(np.float32), kernel_dial, iterations=1).astype(np.int32)
    markers      += 1
    sure_bg       = cv2.dilate(thresh, kernel_dial, iterations=3)
    unknown       = cv2.subtract(sure_bg, np.uint8(np.where(markers > 1, 255, 0)))
    markers[unknown == 255] = 0

    kernel      = np.ones((3, 3), np.uint8)
    gradient    = cv2.morphologyEx(gray_enhanced, cv2.MORPH_GRADIENT, kernel)
    gradient_sq = cv2.multiply(gradient, gradient, scale=1.0 / 255.0)

    markers = cv2.watershed(cv2.cvtColor(gradient_sq, cv2.COLOR_GRAY2BGR), markers)

    img_result     = img_roi_color.copy()
    img_result[markers == -1] = [0, 0, 255]

    unique_markers = np.unique(markers)
    grain_data     = []
    count          = 0
    total_area     = 0

    for label in unique_markers:
        if label <= 1:
            continue
        mask = np.zeros(gray_roi.shape, dtype="uint8")
        mask[markers == label] = 255
        cnts = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)[-2]
        if not cnts:
            continue
        c       = max(cnts, key=cv2.contourArea)
        area_px = cv2.contourArea(c)
        if area_px < min_area:
            continue
        count      += 1
        total_area += area_px
        M = cv2.moments(c)
        if M["m00"] > 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(img_result, str(count), (cX, cY),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 0, 255), 2, cv2.LINE_AA)
            grain_data.append([count, cX, cY, area_px])

    cv2.imwrite(output_path, img_result)

    base_name       = os.path.splitext(os.path.basename(input_path))[0]
    result_csv_path = os.path.join(output_dir, f"{base_name}_report.csv")
    cv2.imwrite(os.path.join(output_dir, "label_map.tif"), markers.astype(np.int32))

    with open(result_csv_path, mode='w', newline='') as file:
        writer = csv.writer(file)
        writer.writerow(['Grain ID', 'Center X', 'Center Y', 'Area (px^2)'])
        writer.writerows(grain_data)

    avg_area = round(total_area / count, 1) if count > 0 else 0.0
    return img_result, count, avg_area


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"ERROR": "Usage: watershed_with_preprocessing.py <input_path> <output_path> [sigmoid_alpha] [sigmoid_beta_pct] [min_area] [dist_thresh_pct] [clahe_clip]"}))
        sys.exit(1)

    input_path       = sys.argv[1]
    output_path      = sys.argv[2]
    sigmoid_alpha    = int(sys.argv[3])   if len(sys.argv) > 3 else 15
    sigmoid_beta_pct = int(sys.argv[4])   if len(sys.argv) > 4 else 13
    min_area         = int(sys.argv[5])   if len(sys.argv) > 5 else 150
    dist_thresh_pct  = int(sys.argv[6])   if len(sys.argv) > 6 else 20
    clahe_clip       = int(sys.argv[7])   if len(sys.argv) > 7 else 3

    result_img, count, avg_area = process_image(
        input_path, output_path,
        sigmoid_alpha, sigmoid_beta_pct, min_area, dist_thresh_pct, clahe_clip)

    if result_img is None:
        print(json.dumps({"ERROR": f"Cannot load file {input_path}"}))
        sys.exit(1)

    print(json.dumps({
        "Ilosc krysztalow": count,
        "Srednia powierzchnia (px2)": round(avg_area, 1),
        "Status": "OK"
    }))
