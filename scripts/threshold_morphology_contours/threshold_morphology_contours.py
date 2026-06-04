import sys
import json
import cv2
import numpy as np

input_path  = sys.argv[1]
output_path = sys.argv[2]

# Optional parameters with defaults
THRESH       = int(sys.argv[3])   if len(sys.argv) > 3 else 80
AREA         = int(sys.argv[4])   if len(sys.argv) > 4 else 15
BLUR_SIZE    = int(sys.argv[5])   if len(sys.argv) > 5 else 5
MORPH_ITER   = int(sys.argv[6])   if len(sys.argv) > 6 else 3
KERNEL_SIZE  = int(sys.argv[7])   if len(sys.argv) > 7 else 3

# Ensure odd values for kernel sizes
if BLUR_SIZE % 2 == 0:
    BLUR_SIZE += 1
if KERNEL_SIZE % 2 == 0:
    KERNEL_SIZE += 1

img = cv2.imread(input_path, cv2.IMREAD_UNCHANGED)
if img is None:
    print(json.dumps({"ERROR": f"Cannot load file {input_path}"}))
    sys.exit(1)

# Convert to 8-bit
if img.dtype == 'uint16':
    img_8bit = (img / 256).astype('uint8')
elif img.dtype == 'uint8':
    img_8bit = img
else:
    img_8bit = cv2.normalize(img, None, 0, 255, cv2.NORM_MINMAX).astype('uint8')

# Convert to grayscale
if len(img_8bit.shape) == 3:
    gray = cv2.cvtColor(img_8bit, cv2.COLOR_BGR2GRAY)
else:
    gray = img_8bit

# Gaussian blur
blur = cv2.GaussianBlur(gray, (BLUR_SIZE, BLUR_SIZE), 0)

# Binary threshold
ret, thresh = cv2.threshold(blur, THRESH, 255, cv2.THRESH_BINARY)

# Morphological opening
kernel  = np.ones((KERNEL_SIZE, KERNEL_SIZE), np.uint8)
opening = cv2.morphologyEx(thresh, cv2.MORPH_OPEN, kernel, iterations=MORPH_ITER)

# Find and draw contours
contours, _ = cv2.findContours(opening, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

img_result = cv2.cvtColor(gray, cv2.COLOR_GRAY2BGR)
count = 0

for cnt in contours:
    area = cv2.contourArea(cnt)
    if area > AREA:
        count += 1
        cv2.drawContours(img_result, [cnt], -1, (0, 255, 0), 2)
        M = cv2.moments(cnt)
        if M["m00"] > 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(img_result, str(count), (cX, cY),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 0, 255), 2, cv2.LINE_AA)

cv2.imwrite(output_path, img_result)

result = {
    "Ilosc krysztalow": count,
    "Status": "OK"
}

print(json.dumps(result))
