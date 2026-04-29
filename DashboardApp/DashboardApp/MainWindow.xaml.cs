using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
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

        public MainWindow()
        {
            InitializeComponent();
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

            ResultsList.Items.Clear();
            int cropBottom = GetCropBottomPx();

            foreach (var path in _loadedImages)
            {
                var result = await ProcessSingleImage(path, cropBottom);
                if (result != null)
                    ResultsList.Items.Add(result);
            }
        }

        private async Task<AnalysisResult?> ProcessSingleImage(string path, int cropBottom)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            int newHeight = Math.Max(1, bmp.PixelHeight - cropBottom);
            var cropped = new CroppedBitmap(bmp, new Int32Rect(0, 0, bmp.PixelWidth, newHeight));

            string tempInput = Path.Combine(Path.GetTempPath(), $"in_{Guid.NewGuid()}.png");
            string tempOutput = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid()}.png");
            SaveBitmapAsPng(cropped, tempInput);

            var json = await RunScript(tempInput, tempOutput);
            if (json == null) return null;

            var imageResult = new BitmapImage(new Uri(tempOutput));

            return new AnalysisResult
            {
                FileName = Path.GetFileName(path),
                ResultImage = imageResult,
                VariablesDisplay = json
            };
        }

        private static void SaveBitmapAsPng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(path, FileMode.Create);
            encoder.Save(fs);
        }

        private async Task<string?> RunScript(string inputPath, string outputPath)
        {
            string scriptPath = @"C:\Users\Filip\Desktop\Studia\Projekt-Grupowy-ID419\scripts\threshold_morphology_contours\threshold_morphology_contours.py";

            var psi = new ProcessStartInfo("py", $"\"{scriptPath}\" \"{inputPath}\" \"{outputPath}\"")
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

        public class AnalysisResult
        {
            public string FileName { get; set; } = "";
            public ImageSource ResultImage { get; set; } = null!;
            public string VariablesDisplay { get; set; } = "";
        }
    }
}
