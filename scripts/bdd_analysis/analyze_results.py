import cv2
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import csv
import os
import sys
import json

if len(sys.argv) < 3:
    print(json.dumps({"ERROR": "Usage: analyze_results.py <input_label_map> <output_histogram_path>"}))
    sys.exit(1)

INPUT_MAP = sys.argv[1]
output_path = sys.argv[2]
OUTPUT_DIR = os.path.dirname(output_path) or '.'

SCALE_PX_PER_UNIT = 680.0
UNIT = 'um'
MIN_AREA_PHYS = 50.0 / (SCALE_PX_PER_UNIT * SCALE_PX_PER_UNIT)

os.makedirs(OUTPUT_DIR, exist_ok=True)

# Loading the label map (we use IMREAD_UNCHANGED to load 32-bit TIFF correctly)
label_map = cv2.imread(INPUT_MAP, cv2.IMREAD_UNCHANGED)

if label_map is None:
    print(f"Error: Unable to load file: {INPUT_MAP}!")
    exit(1)

# Number grains and their pixel counts
unique_ids, pixel_counts = np.unique(label_map, return_counts=True)

grain_data = []
areas_physical = []

# For converting from px^2 to unit^2
AREA_CONVERSION_FACTOR = SCALE_PX_PER_UNIT ** 2

for grain_id, px_count in zip(unique_ids, pixel_counts):
    # Skipping background
    if grain_id <= 1:
        continue

    area_phys = px_count / AREA_CONVERSION_FACTOR

    # Noise filtering
    if area_phys >= MIN_AREA_PHYS:
        grain_data.append([grain_id, px_count, round(area_phys, 5)])
        areas_physical.append(area_phys)

# Statistical calculations
valid_grain_count = len(areas_physical)

if valid_grain_count == 0:
    print(json.dumps({"ERROR": "Haven't found any valid grains"}))
    sys.exit(0)

areas_physical = np.array(areas_physical)

mean_area = np.mean(areas_physical)
median_area = np.median(areas_physical)
std_dev = np.std(areas_physical)
min_area = np.min(areas_physical)
max_area = np.max(areas_physical)

# Saving results to csv file
csv_path = os.path.join(OUTPUT_DIR, 'statistical_report.csv')
with open(csv_path, mode='w', newline='') as file:
    writer = csv.writer(file)
    writer.writerow(['General Summary:'])
    writer.writerow(['Number of segments', valid_grain_count])
    writer.writerow([f'Average Area [{UNIT}^2]', round(mean_area, 5)])
    writer.writerow([f'Median area [{UNIT}^2]', round(median_area, 5)])
    writer.writerow([f'Standard deviation [{UNIT}^2]', round(std_dev, 5)])
    writer.writerow([''])
    writer.writerow(['Grain ID', 'Area (px)', f'Area ({UNIT}^2)'])
    writer.writerows(grain_data)

# Generating the histogram
plt.figure(figsize=(10, 6))

# Freedman–Diaconis
q75, q25 = np.percentile(areas_physical, [75, 25])
iqr = q75 - q25

if iqr == 0:
    bins = 10  # fallback
else:
    h = 2 * iqr / (len(areas_physical) ** (1 / 3))

    if h == 0:
        bins = 10
    else:
        bins = int((areas_physical.max() - areas_physical.min()) / h)
        bins = max(10, bins) # to get at least 10 bins

plt.hist(areas_physical, bins=bins, color='skyblue', edgecolor='black', alpha=0.7)
plt.title('Grains size distribution', fontsize=16)
plt.xlabel(f'Grain area [{UNIT}$^2$]', fontsize=14)
plt.ylabel('Number of grains', fontsize=14)
plt.grid(axis='y', linestyle='--', alpha=0.7)

# Dodanie pionowych linii ze średnią i medianą
plt.axvline(mean_area, color='red', linestyle='dashed', linewidth=2, label=f'Average: {mean_area:.5f}')
plt.axvline(median_area, color='green', linestyle='dashed', linewidth=2, label=f'Median: {median_area:.5f}')
plt.legend()

plt.savefig(output_path, dpi=300, bbox_inches='tight')

print(json.dumps({
    "Ilosc krysztalow": valid_grain_count,
    "Srednia powierzchnia (px2)": round(mean_area, 5),
    "Status": "OK"
}))