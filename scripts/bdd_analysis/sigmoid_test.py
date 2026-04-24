import cv2
import numpy as np

IMAGE_PATH = 'results//debug_steps//03_MedianBlur_applied.png'


def sigmoid_contrast(image, alpha, beta):
    img_normalized = image / 255.0
    sigmoid = 1 / (1 + np.exp(-alpha * (img_normalized - beta)))
    return np.uint8(cv2.normalize(sigmoid, None, 0, 255, cv2.NORM_MINMAX))

def nothing(x):
    pass

img = cv2.imread(IMAGE_PATH, cv2.IMREAD_GRAYSCALE)
if img is None:
    print("Błąd: Nie znaleziono pliku!")
    exit()

#gray = cv2.medianBlur(img_raw, 5)

cv2.namedWindow('Kalibracja', cv2.WINDOW_NORMAL)
cv2.resizeWindow('Kalibracja', 800, 600)

cv2.createTrackbar('Alpha (Sila)', 'Kalibracja', 15, 30, nothing)
cv2.createTrackbar('Beta (Prog)', 'Kalibracja', 10, 100, nothing)

print("Instrukcja: Przesuwaj suwaki. Naciśnij 'ESC', aby zamknąć i wypisać parametry.")

while True:
    a = cv2.getTrackbarPos('Alpha (Sila)', 'Kalibracja')
    b = cv2.getTrackbarPos('Beta (Prog)', 'Kalibracja') / 100.0

    if a == 0: a = 0.1

    result = sigmoid_contrast(img, a, b)

    cv2.imshow('Kalibracja', result)

    if cv2.waitKey(1) & 0xFF == 27:
        print(f"\nWybrane parametry: Alpha={a}, Beta={b:.2f}")
        break

cv2.destroyAllWindows()