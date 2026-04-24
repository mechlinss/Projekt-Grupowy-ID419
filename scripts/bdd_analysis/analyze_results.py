import cv2
import numpy as np
import matplotlib.pyplot as plt
import csv
import os
import argparse

# --- Reading arguments from console  ---

parser = argparse.ArgumentParser(description='Statistical analysis of grains from the label map.')
parser.add_argument('--input', type=str, default='results/label_map.tif', help='Path to the label map')
parser.add_argument('--output_dir', type=str, default='results', help='Directory to save the results')
parser.add_argument('--scale', type=float, default=1.0, help='Scale: Number of pixels per 1 physical unit (e.g. pixels per 1 um)')
parser.add_argument('--unit', type=str, default='px', help='Unit symbol (np. um, mm)')
parser.add_argument('--min_area', type=float, default=0.0, help='Minimum grain size (in physical units) for noise rejection')

args = parser.parse_args()

INPUT_MAP = args.input
OUTPUT_DIR = args.output_dir
SCALE_PX_PER_UNIT = args.scale
UNIT = args.unit
MIN_AREA_PHYS = args.min_area

# Hardcoded values for debugging
INPUT_MAP = 'results/label_map.tif'
OUTPUT_DIR = 'results'
SCALE_PX_PER_UNIT = 680.0
UNIT = 'um'
MIN_AREA_PHYS = 50.0/(SCALE_PX_PER_UNIT*SCALE_PX_PER_UNIT)

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
    print("Warning: Haven't found any valid grains!")
    exit(0)

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

# Zapis wykresu
plot_path = os.path.join(OUTPUT_DIR, 'distribution_histogram.png')
plt.savefig(plot_path, dpi=300, bbox_inches='tight')

# Printing results to the console
print("=== ANALYSIS RESULTS ===")
print(f"Number of segments: {valid_grain_count}")
print(f"Average Area: {mean_area:.5f} {UNIT}^2")
print(f"Median Area: {median_area:.5f} {UNIT}^2")
print(f"Standard deviation: {std_dev:.5f} {UNIT}^2")
print(f"Smallest grain: {min_area:.5f} {UNIT}^2")
print(f"Biggest grain: {max_area:.5f} {UNIT}^2")
print("======================")
print(f"CSV report saved to: {csv_path}")
print(f"Histogram saved to: {plot_path}")