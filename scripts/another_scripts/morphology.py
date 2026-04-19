import cv2
import numpy as np
from IPython.display import display, Image
import matplotlib.pyplot as plt

# ---------- Display function ----------
def imshow(img, ax=None):
    if ax is None:
        ret, encoded = cv2.imencode(".jpg", img)
        display(Image(encoded.tobytes()))
    else:
        ax.imshow(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
        ax.axis('off')



def find_crystals(photo_path: str):

    img = cv2.imread(photo_path) 
    # Grayscale
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # Detect black background
    _, background = cv2.threshold(gray, 40, 255, cv2.THRESH_BINARY)
    # Clean background mask 
    kernel = np.ones((3,3), np.uint8)
    background = cv2.morphologyEx(background, cv2.MORPH_OPEN, kernel, iterations=2)
    background = cv2.morphologyEx(background, cv2.MORPH_CLOSE, kernel, iterations=2)
    # Morphological edge detection (black -> anything transition)
    eroded = cv2.erode(background, kernel, iterations=1)
    edges = cv2.subtract(background, eroded)
    # Find contours from these edges
    contours, _ = cv2.findContours(
        edges,
        cv2.RETR_EXTERNAL,
        cv2.CHAIN_APPROX_SIMPLE
    )

    # Filter contours 
    diamonds = []
    for c in contours:
        if cv2.contourArea(c) > 50:
            diamonds.append(c)
    result = img.copy()
    cv2.drawContours(result, diamonds, -1, (0,255,0), 2)

    # imshow(img)
    # imshow(background)
    # imshow(edges)
    # imshow(result)
    return result, len(diamonds)



if __name__ == "__main__":
    img, amount_of_crystals = find_crystals("BDD\\Standard\\2911_001.tif")
    imshow(img)
    print("Liczba wykrytych diamentów: ", amount_of_crystals)
