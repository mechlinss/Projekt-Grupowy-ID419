import sys
import json
import cv2
import numpy as np

input_path = sys.argv[1]
output_path = sys.argv[2]

THRESH = 80
AREA = 15

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

# Analyse
blur = cv2.GaussianBlur(gray, (5, 5), 0)
ret, thresh = cv2.threshold(blur, THRESH, 255, cv2.THRESH_BINARY)

kernel = np.ones((3,3), np.uint8)
opening = cv2.morphologyEx(thresh, cv2.MORPH_OPEN, kernel, iterations=3)

contours, _ = cv2.findContours(opening, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

img_result = cv2.cvtColor(gray, cv2.COLOR_GRAY2BGR)
count = 0

for cnt in contours:
    area = cv2.contourArea(cnt)
    if area > AREA:
        count += 1
        cv2.drawContours(img_result, [cnt], -1, (0, 255, 0), 2)

cv2.imwrite(output_path, img_result)

result = {
    "ZMIENNA1": count,
    "ZMIENNA2": int(ret),
    "ZMIENNA3": "OK"
}

print(json.dumps(result))