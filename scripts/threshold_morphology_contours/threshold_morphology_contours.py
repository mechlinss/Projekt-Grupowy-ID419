import cv2
import numpy as np
import matplotlib.pyplot as plt

IMAGE_PATH = '4075_006.tif'
THRESH = 80
AREA = 15

img = cv2.imread(IMAGE_PATH, cv2.IMREAD_UNCHANGED)

if img is None:
    print(f"Error: Cannot load file {IMAGE_PATH}")
    exit()

r = cv2.selectROI("Select area (Confirm with ENTER)", img, showCrosshair=True)
cv2.destroyAllWindows()

if r[2] > 0 and r[3] > 0:
    img = img[int(r[1]):int(r[1]+r[3]), int(r[0]):int(r[0]+r[2])]
    print(f"Successfully cropped image to size: {img.shape}")
else:
    print("No area selected or cancelled. Using the entire image.")

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

# Blurring (noise reduction before binarization)
blur = cv2.GaussianBlur(gray, (5, 5), 0)

# If the background is bright and objects are dark, use cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU
threshold = 80
ret, thresh = cv2.threshold(blur, threshold, 255, cv2.THRESH_BINARY)

# Removes small white dots (noise) and separates objects connected by thin bridges
kernel = np.ones((3,3), np.uint8)

# Increase the number of iterations if objects are heavily glued together or there is a lot of noise
opening = cv2.morphologyEx(thresh, cv2.MORPH_OPEN, kernel, iterations=3) 


contours, hierarchy = cv2.findContours(opening, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)


img_result = cv2.cvtColor(gray, cv2.COLOR_GRAY2BGR)
count = 0

for i, cnt in enumerate(contours):
    area = cv2.contourArea(cnt)
    if area > AREA:
        count += 1
        cv2.drawContours(img_result, [cnt], -1, (0, 255, 0), 2)
        
        M = cv2.moments(cnt)
        if M["m00"] != 0:
            cX = int(M["m10"] / M["m00"])
            cY = int(M["m01"] / M["m00"])
            cv2.putText(img_result, str(count), (cX - 10, cY), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 2)

print(f"--- RESULT ---")
print(f"Objects found: {count}")

#cv2.imwrite("threshold_only_mask.png", opening)
#cv2.imwrite("threshold_only_result.png", img_result)

plt.figure(figsize=(10, 5))
plt.subplot(1, 2, 1)
plt.title("Binary mask (after morphology)")
plt.imshow(opening, cmap='gray')
plt.axis('off')

plt.subplot(1, 2, 2)
plt.title(f"Result: {count} objects")
plt.imshow(cv2.cvtColor(img_result, cv2.COLOR_BGR2RGB))
plt.axis('off')

plt.tight_layout()
plt.show()