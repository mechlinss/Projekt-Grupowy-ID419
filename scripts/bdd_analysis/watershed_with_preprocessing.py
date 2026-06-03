import cv2
import numpy as np
import matplotlib.pyplot as plt
import csv
import os

IMAGE_PATH = 'Ax2768_001.tif'
OUTPUT_DIR = 'results'
DEBUG_DIR = os.path.join(OUTPUT_DIR, 'debug_steps')

os.makedirs(OUTPUT_DIR, exist_ok=True)
os.makedirs(DEBUG_DIR, exist_ok=True)

# --- PARAMETERS ---
SIGMOID_ALPHA = 15.0
SIGMOID_BETA = 0.13
MIN_AREA_PX = 150
MAX_SCREEN_HEIGHT = 800


# --- AUXILIARY FUNCTIONS---
def apply_sigmoid_contrast(image, alpha, beta):
    """Applies nonlinear sigmoidal contrast to a uint8 image"""
    img_float = image / 255.0
    sigmoid = 1 / (1 + np.exp(-alpha * (img_float - beta)))
    return np.uint8(cv2.normalize(sigmoid, None, 0, 255, cv2.NORM_MINMAX))


def save_debug_image(step_number, name, image, is_float=False):
    """Normalizes and saves the image from a given step to the debug folder"""
    filename = f"{step_number:02d}_{name}.png"
    filepath = os.path.join(DEBUG_DIR, filename)

    if is_float or image.dtype != np.uint8:
        norm_img = cv2.normalize(image, None, 0, 255, cv2.NORM_MINMAX, dtype=cv2.CV_8U)
        cv2.imwrite(filepath, norm_img)
    else:
        cv2.imwrite(filepath, image)


# --- LOADING AN IMAGE AND CHOOSING THE REGION OF INTERESTS ---
img_raw = cv2.imread(IMAGE_PATH)
if img_raw is None:
    print(f"Error: Unable to load an image: {IMAGE_PATH}")
    exit()

h, w = img_raw.shape[:2]
gray = cv2.cvtColor(img_raw, cv2.COLOR_BGR2GRAY)

if h > MAX_SCREEN_HEIGHT:
    scale = MAX_SCREEN_HEIGHT / h
    img_display = cv2.resize(gray, (int(w * scale), int(h * scale)))
else:
    scale = 1.0
    img_display = gray.copy()

window_name = "Select area (ENTER), ESC to use whole image"
r = cv2.selectROI(window_name, img_display, showCrosshair=True)
cv2.destroyAllWindows()

if r[2] > 0 and r[3] > 0:
    x_orig = int(r[0] / scale)
    y_orig = int(r[1] / scale)
    w_orig = int(r[2] / scale)
    h_orig = int(r[3] / scale)
    gray_roi = gray[y_orig: y_orig + h_orig, x_orig: x_orig + w_orig]
    img_roi_color = img_raw[y_orig: y_orig + h_orig, x_orig: x_orig + w_orig]
    print(f"Successfully cropped image to size: {gray_roi.shape}")
else:
    print("Using an entire image.")
    gray_roi = gray
    img_roi_color = img_raw.copy()

save_debug_image(1, "Original", gray_roi)

# --- PREPROCESSING ---
# Histogram equalization (leveling out lighting irregularities)
# Adjust the contrast in each part tile of the tileGridSize separately
clahe = cv2.createCLAHE(clipLimit=3.0, tileGridSize=(32, 32))
gray_clahe = clahe.apply(gray_roi)
save_debug_image(2, "CLAHE_applied", gray_clahe)

# Noise removal
gray_blur = cv2.medianBlur(gray_clahe, 5)
save_debug_image(3, "MedianBlur_applied", gray_blur)

# Contrast enhancement by the sigmoid function
gray_enhanced = apply_sigmoid_contrast(gray_blur, SIGMOID_ALPHA, SIGMOID_BETA)
save_debug_image(4, "Sigmoid_Contrast", gray_enhanced)

# --- WATERSHED ---
# Otsu thresholding - automatically calculates optimal threshold value
ret, thresh = cv2.threshold(gray_enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
save_debug_image(5, "Otsu_Thresh", thresh)

# For each white pixel (seed) calculates distance from the nearest black one (background)
dist_transform = cv2.distanceTransform(thresh, cv2.DIST_L2, 5)
save_debug_image(6, "Distance_Map", dist_transform, is_float=True)

# Blurring the distance map prevents seeds from splitting into smaller ones
# The bigger the kernel size is, the bigger the segments that will be determined
dist_smooth = cv2.GaussianBlur(dist_transform, (21, 21), 0)
save_debug_image(7, "Distance_Map_Blurred", dist_smooth, is_float=True)

# Selecting starting seeds
_, peaks = cv2.threshold(dist_smooth, 0.2 * dist_smooth.max(), 255, 0)
peaks = np.uint8(peaks)
save_debug_image(8, "Starting_Points", peaks)

# Assigning unique ID for each seed
_, markers = cv2.connectedComponents(peaks)

kernel_dial = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
markers = cv2.dilate(markers.astype(np.float32), kernel_dial, iterations=1)
markers = markers.astype(np.int32)

save_debug_image(9, "Markers_After_Dilatation", markers, is_float=True)

# Preparing area of uncertainty for watershed algorithm
markers = markers + 1
sure_bg = cv2.dilate(thresh, kernel_dial, iterations=3)# number of iterations will define the size of unknown area
unknown = cv2.subtract(sure_bg, np.uint8(np.where(markers > 1, 255, 0)))
# unknown is an area around the background
markers[unknown == 255] = 0 # area marked by 0 is a working space for watershed algorithm
save_debug_image(10, "Unknown_Area", unknown)


kernel = np.ones((3, 3), np.uint8)
# Using gradient to create "walls" used in watershed
gradient = cv2.morphologyEx(gray_enhanced, cv2.MORPH_GRADIENT, kernel)
gradient_sq = cv2.multiply(gradient, gradient, scale=1.0 / 255.0) #making the walls steeper
save_debug_image(11, "Gradient_Edges", gradient_sq, is_float=True)

# Running the watershed algorithm - "0" from the markers matrix will be replaced with id of best matching seed
# Borders will be marked as -1
markers = cv2.watershed(cv2.cvtColor(gradient_sq, cv2.COLOR_GRAY2BGR), markers)

# --- DATA EXTRACTION AND FILTERING ---
img_result = img_roi_color.copy()
img_result[markers == -1] = [0, 0, 255]

unique_markers = np.unique(markers)
grain_data = []
count = 0

for label in unique_markers:
    # Skipping background and borders
    if label <= 1:
        continue

    mask = np.zeros(gray_roi.shape, dtype="uint8")
    mask[markers == label] = 255
    cnts = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)[-2]

    if len(cnts) > 0:
        c = max(cnts, key=cv2.contourArea)
        area_px = cv2.contourArea(c)

        if area_px < MIN_AREA_PX:
            continue

        count += 1
        M = cv2.moments(c)
        if M["m00"] > 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(img_result, str(count), (cX, cY),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 255, 0), 1)
            grain_data.append([count, cX, cY, area_px])

# --- SAVING RESULTS AND PRINTING SUMMARY ---
base_name = os.path.splitext(os.path.basename(IMAGE_PATH))[0]
result_img_path = os.path.join(OUTPUT_DIR, f"{base_name}_result.png")
result_csv_path = os.path.join(OUTPUT_DIR, f"{base_name}_report.csv")

# Raw data result
cv2.imwrite(os.path.join(OUTPUT_DIR, "label_map.tif"), markers.astype(np.int32))
cv2.imwrite(result_img_path, img_result)

with open(result_csv_path, mode='w', newline='') as file:
    writer = csv.writer(file)
    writer.writerow(['Grain ID', 'Center X', 'Center Y', 'Area (px^2)'])
    writer.writerows(grain_data)

print(f"\n--- ANALYSIS COMPLETED---")
print(f"Grains found: {count}")
print(f"Result image saved in: {result_img_path}")
print(f"Intermediate steps (debug) saved in: {DEBUG_DIR}")

if count > 0:
    total_area = sum(d[3] for d in grain_data)
    avg_area = total_area / count
    print(f"Average grain area: {avg_area:.1f} px^2")

plt.imshow(peaks, cmap='gray')
plt.title("Seeds density")
plt.show()