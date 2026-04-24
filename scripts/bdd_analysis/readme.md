## Structure & Scripts

### 1. watershed_with_preprocessing.py
This is the core script of the project. It performs advanced image segmentation based on the Marker-controlled Watershed algorithm applied to a morphological gradient.




**Outputs:**
* `results/label_map.tif`: A 32-bit TIFF file where each pixel value corresponds to a unique grain ID.
* `results/[image_name]_report.csv`: Initial report containing grain IDs and their pixel areas.
* `results/[image_name]_wynik.png`: Visual preview with detected grain boundaries drawn in red.
* `debug_steps/`: A directory containing intermediate images (01 to 11) representing every stage of the pipeline (CLAHE, Sigmoid, Distance Transform, etc.).

---

### 2. sigmoid_test.py
An interactive calibration tool used to determine the optimal `Alpha` and `Beta` parameters for the Sigmoid contrast function.

* **Input:** For best results and consistency, use the `03_MedianBlur_applied.png` image from the `debug_steps` directory as input, as this is the exact stage where the sigmoid operation is applied in the main script.
* **Functionality:** Provides real-time sliders to adjust contrast strength (`Alpha`) and the cutoff point (`Beta`).

---

### 3. analyze_results.py
It processes the finalized label map (potentially after manual correction) to generate physical measurements.

* **Inputs:** Requires `label_map.tif` and physical scale parameters (pixels per unit).
* **Calculations:**
    * Total number of segments.
    * Average and median grain area.
    * Standard deviation.
    * Minimum and maximum area distribution.
* **Outputs:**
    * `statistical_report.csv`: Comprehensive physical data for all grains.
    * `distribution_histogram.png`: A high-resolution histogram showing the grain size distribution with mean and median markers.

---

## Configuration (`parameters.json`)

Currently, the configuration file contains parameters optimized for a specific test image. However, the system is designed for future extensibility; these parameters can be generalized to cover broader groups of similar images, and additional profiles can be defined

**Suggested parameter structure:**
```json
{
  "profiles": {
    "Ax_2786_001": {
      "description": "Ax_2786_001.tif",
      "clahe_clip_limit": 3.0,
      "clahe_tile_grid_size": 32,
      "median_blur": 5,
      "sigmoid_alpha": 15.0,
      "sigmoid_beta": 0.13,
      "dist_blur": 21,
      "dist_threshold": 0.2,
      "dilation_iter_markers": 1,
      "dilatation_iter_sure_bg": 3,
      "min_area_px": 150
    }
  }
}