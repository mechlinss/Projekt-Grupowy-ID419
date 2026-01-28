# Projekt-Grupowy-ID419

A local application for analyzing Scanning Electron Microscopy (SEM) images.

## Prerequisites

- Python 3.7 or higher
- Windows operating system
- Virtual environment (venv)

## Installation & Setup

### 1. Navigate to the scripts directory

```bash
cd scripts
```

### 2. Activate the virtual environment

```bash
.venv\Scripts\activate
```

> **Note:** If you encounter an execution policy error, run PowerShell as Administrator and execute:
> ```powershell
> Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
> ```

### 3. Navigate to your desired analysis method

```bash
cd <directory_name>
```

Replace `<directory_name>` with the specific analysis method you want to use.

### 4. Install required dependencies

```bash
pip install -r requirements.txt
```

### 5. Run the Python script

```bash
python <script_name>.py
```

Replace `<script_name>` with the actual name of the Python script you want to execute.

## Analyzing Your Own Images

To analyze your own SEM images:

1. Copy your image file to the script's directory
2. Open the Python script in a text editor
3. Update the `FILEPATH` variable with your image filename:
   ```python
   FILEPATH = "your_image_name.tif"  # or .png, .jpg, etc.
   ```
4. Save the file and run the script again

## Deactivating the Virtual Environment

When you're done working, deactivate the virtual environment:

```bash
deactivate
```

## Troubleshooting

### Virtual environment not activating
- Ensure you're in the correct directory (`scripts`)
- Check that the `.venv` folder exists
- Verify Python is properly installed and added to PATH

### Module not found errors
- Make sure you've activated the virtual environment
- Reinstall requirements: `pip install -r requirements.txt`

### Image file not found
- Verify the image file is in the same directory as the script
- Check that the `FILEPATH` variable matches your filename exactly (including extension)
- Ensure the file path uses the correct format

## Project Structure

```
scripts/
├── .venv/                  # Virtual environment
├── <analysis_method_1>/    # Analysis method directory
│   ├── script.py          # Python script
│   ├── requirements.txt   # Dependencies
│   └── sample_image.jpg   # Sample SEM image
├── <analysis_method_2>/    # Another analysis method
│   └── ...
└── README.md              # This file
```

