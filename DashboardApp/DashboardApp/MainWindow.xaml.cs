using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace DashboardApp
{
    public partial class MainWindow : Window
    {
        private List<string> _loadedImages = new();
        private BitmapSource? _originalBitmap;
        private string? _originalPath;

        private Point _dragStart;
        private bool _isDragging;
        private Rect _cropRect;

        private record ScriptEntry(string DisplayName, string RelativePath, List<ParameterDef> Params);

        private static List<ParameterDef> P(params ParameterDef[] defs) => new(defs);

        private readonly List<ScriptEntry> _scripts = new()
        {
            new("Threshold + Morphology + Contours",
                @"scripts\threshold_morphology_contours\threshold_morphology_contours.py",
                P(
                    new("THRESH",      "Próg binaryzacji",          0,   255, 80),
                    new("AREA",        "Min. obszar konturu (px²)", 1,   500, 15),
                    new("BLUR",        "Rozmycie Gaussa (kernel)",  1,   9,   5, 2, true),
                    new("MORPH_ITER",  "Iteracje morfologii",       1,   10,  3, 1, true),
                    new("KERNEL",      "Rozmiar kernela morfologii",3,   7,   3, 2, true)
                )),

            new("Morphology",
                @"scripts\another_scripts\morphology.py",
                P(
                    new("THRESH",      "Próg binaryzacji",          0,   255, 40),
                    new("MIN_AREA",    "Min. obszar konturu (px²)", 1,   500, 50),
                    new("KERNEL",      "Rozmiar kernela",           3,   7,   3, 2, true),
                    new("OPEN_ITER",   "Iteracje otwarcia",         1,   10,  2, 1, true),
                    new("CLOSE_ITER",  "Iteracje zamknięcia",       1,   10,  2, 1, true)
                )),

            new("Canny Edge Detection",
                @"scripts\another_scripts\canny.py",
                P(
                    new("CANNY_T1",    "Próg Canny dolny",          0,   255, 80),
                    new("CANNY_T2",    "Próg Canny górny",          0,   255, 90),
                    new("KERNEL",      "Rozmiar kernela",           3,   7,   3, 2, true),
                    new("CLOSE_ITER",  "Iteracje zamknięcia",       1,   10,  2, 1, true),
                    new("MIN_AREA",    "Min. obszar konturu (px²)", 1,   500, 50)
                )),

            new("Thresholding",
                @"scripts\another_scripts\thresholding.py",
                P(
                    new("THRESH",      "Próg binaryzacji",          0,   255, 60),
                    new("MIN_AREA",    "Min. obszar konturu (px²)", 1,   500, 50),
                    new("KERNEL",      "Rozmiar kernela",           3,   7,   3, 2, true),
                    new("OPEN_ITER",   "Iteracje otwarcia",         1,   10,  2, 1, true),
                    new("CLOSE_ITER",  "Iteracje zamknięcia",       1,   10,  2, 1, true)
                )),

            new("Watershed (basic)",
                @"scripts\another_scripts\watershed.py",
                P(
                    new("THRESH",      "Próg binaryzacji",          0,   255, 150),
                    new("KERNEL",      "Rozmiar kernela",           3,   7,   3, 2, true),
                    new("OPEN_ITER",   "Iteracje otwarcia",         1,   10,  2, 1, true),
                    new("DIST_PCT",    "Próg odległości (%)",       5,   50,  10, 1, true)
                )),

            new("Watershed + Preprocessing",
                @"scripts\bdd_analysis\watershed_with_preprocessing.py",
                P(
                    new("SIG_ALPHA",   "Sigmoid Alpha (siła)",      1,   30,  15),
                    new("SIG_BETA",    "Sigmoid Beta×100 (próg)",   1,   50,  13),
                    new("MIN_AREA",    "Min. obszar (px²)",         10,  500, 150),
                    new("DIST_PCT",    "Próg odległości (%)",       5,   50,  20, 1, true),
                    new("CLAHE_CLIP",  "CLAHE clip limit",          1,   10,  3,  1, true)
                )),
        };

        private string ProjectRoot =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\"));

        private string PythonExe
        {
            get
            {
                var venvPython = Path.Combine(ProjectRoot, @"scripts\venv\Scripts\python.exe");
                return File.Exists(venvPython) ? venvPython : "py";
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            PopulateScriptSelector();
        }

        private void PopulateScriptSelector()
        {
            foreach (var script in _scripts)
                ScriptSelector.Items.Add(script.DisplayName);
            ScriptSelector.SelectedIndex = 0;
        }

        private string GetSelectedScriptPath()
        {
            int idx = ScriptSelector.SelectedIndex;
            if (idx < 0 || idx >= _scripts.Count)
                idx = 0;
            return Path.Combine(ProjectRoot, _scripts[idx].RelativePath);
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Wybierz zdjęcia",
                Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp|Wszystkie pliki|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
                LoadImages(dlg.FileNames);
        }

        private void LoadImage(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                _originalBitmap = bmp;
                _originalPath = path;

                BtnCrop.IsEnabled = false;

                ImageSizeLabel.Text = $"{bmp.PixelWidth} × {bmp.PixelHeight} px";
                InfoLabel.Text = $"{Path.GetFileName(path)}  —  zaznacz obszar do przycięcia";
                StatusLabel.Text = $"Wczytano: {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania pliku:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadImages(string[] paths)
        {
            _loadedImages = paths.ToList();
            InfoLabel.Text = $"Załadowano: {_loadedImages.Count} plików";
            StatusLabel.Text = "Gotowy do przycięcia";
            BtnCrop.IsEnabled = _loadedImages.Count > 0;
        }
        private int GetCropBottomPx()
        {
            if (int.TryParse(CropBottomBox.Text, out var px) && px >= 0)
                return px;
            return 0;
        }

        private void Canvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Canvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                    LoadImage(files[0]);
            }
        }

        private async void BtnCrop_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedImages.Count == 0) return;

            int selectedIdx = ScriptSelector.SelectedIndex;
            var scriptEntry = _scripts[selectedIdx < 0 ? 0 : selectedIdx];
            string croppedTemp = CropImageToTemp(_loadedImages[0], GetCropBottomPx());

            var preview = new LivePreviewWindow(
                croppedTemp,
                GetSelectedScriptPath(),
                PythonExe,
                scriptEntry.DisplayName,
                scriptEntry.Params);
            preview.Owner = this;

            preview.AnalysisConfirmed += async (ScriptParams p) =>
            {
                ResultsList.Items.Clear();
                int cropBottom = GetCropBottomPx();
                StatusLabel.Text  = "Analizuję wszystkie zdjęcia...";
                BtnCrop.IsEnabled = false;

                foreach (var path in _loadedImages)
                {
                    var result = await ProcessSingleImage(path, cropBottom, p);
                    if (result != null)
                        ResultsList.Items.Add(result);
                }

                StatusLabel.Text  = $"Gotowe — przeanalizowano {ResultsList.Items.Count} zdjęć";
                BtnCrop.IsEnabled = true;
            };

            preview.Show();
        }

        private async Task<AnalysisResult?> ProcessSingleImage(string path, int cropBottom, ScriptParams? p = null)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            int newHeight = Math.Max(1, bmp.PixelHeight - cropBottom);
            var cropped = new CroppedBitmap(bmp, new Int32Rect(0, 0, bmp.PixelWidth, newHeight));

            string tempInput  = Path.Combine(Path.GetTempPath(), $"in_{Guid.NewGuid()}.png");
            string tempOutput = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid()}.png");
            SaveBitmapAsPng(cropped, tempInput);

            var json = await RunScript(tempInput, tempOutput, p);
            if (json == null) return null;

            if (!File.Exists(tempOutput))
                return new AnalysisResult
                {
                    FileName = Path.GetFileName(path),
                    ResultImage = new BitmapImage(new Uri(path)),
                    VariablesDisplay = $"Błąd: skrypt nie zapisał obrazu wynikowego\n{json}"
                };

            var imageResult = new BitmapImage(new Uri(tempOutput));

            return new AnalysisResult
            {
                FileName = Path.GetFileName(path),
                ResultImage = imageResult,
                VariablesDisplay = json
            };
        }

        private static string CropImageToTemp(string imagePath, int cropBottom)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(imagePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            int newHeight = Math.Max(1, bmp.PixelHeight - cropBottom);
            var cropped = new CroppedBitmap(bmp, new Int32Rect(0, 0, bmp.PixelWidth, newHeight));

            string tempPath = Path.Combine(Path.GetTempPath(), $"preview_in_{Guid.NewGuid()}.png");
            SaveBitmapAsPng(cropped, tempPath);
            return tempPath;
        }

        private static void SaveBitmapAsPng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(path, FileMode.Create);
            encoder.Save(fs);
        }

        private async Task<string?> RunScript(string inputPath, string outputPath, ScriptParams? p = null)
        {
            string scriptPath = GetSelectedScriptPath();

            string extraArgs = p is not null ? " " + p.ToArgString() : "";

            var psi = new ProcessStartInfo(PythonExe, $"\"{scriptPath}\" \"{inputPath}\" \"{outputPath}\"{extraArgs}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            string output = await proc.StandardOutput.ReadToEndAsync();
            string error = await proc.StandardError.ReadToEndAsync();
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "script_stderr.txt"), error);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "script_stdout.txt"), output);
            proc.WaitForExit(30_000);

            if (proc.ExitCode != 0)
                return $"Błąd: {error}";

            return output.Trim();
        }

        private void ResultsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is AnalysisResult result)
                ShowDetail(result);
        }

        private void ShowDetail(AnalysisResult result)
        {
            DetailImage.Source = result.ResultImage;
            DetailFileName.Text = result.FileName;
            DetailParams.Text = FormatParams(result.VariablesDisplay);
            DetailOverlay.Visibility = Visibility.Visible;
        }

        private static string FormatParams(string raw)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw);
                var sb = new System.Text.StringBuilder();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    sb.AppendLine($"{prop.Name}:  {prop.Value}");
                return sb.ToString().TrimEnd();
            }
            catch
            {
                return raw;
            }
        }

        private void Overlay_BackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DetailOverlay.Visibility = Visibility.Collapsed;
        }

        private void Overlay_PanelClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void BtnCloseDetail_Click(object sender, RoutedEventArgs e)
        {
            DetailOverlay.Visibility = Visibility.Collapsed;
        }

        public class AnalysisResult
        {
            public string FileName { get; set; } = "";
            public ImageSource ResultImage { get; set; } = null!;
            public string VariablesDisplay { get; set; } = "";
        }
    }
}
