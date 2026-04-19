using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace DashboardApp
{
    public partial class MainWindow : Window
    {
        private BitmapSource? _originalBitmap;
        private string? _originalPath;

        private Point _dragStart;
        private bool _isDragging;
        private Rect _cropRect;
        private bool _hasCrop;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Wybierz zdjęcie",
                Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp|Wszystkie pliki|*.*"
            };
            if (dlg.ShowDialog() == true)
                LoadImage(dlg.FileName);
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

                ImageDisplay.Source = bmp;

                CropCanvas.Width = bmp.PixelWidth;
                CropCanvas.Height = bmp.PixelHeight;

                ResetCrop();
                HideDropZone();

                BtnReset.IsEnabled = true;
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

        private void HideDropZone()
        {
            DropZone.Visibility = Visibility.Collapsed;
            ImageScrollViewer.Visibility = Visibility.Visible;
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


        private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_originalBitmap == null) return;
            _dragStart = e.GetPosition(CropCanvas);
            _isDragging = true;
            _hasCrop = false;
            CropCanvas.CaptureMouse();
            UpdateCropVisuals(new Rect(_dragStart, _dragStart));
        }

        private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var pos = e.GetPosition(CropCanvas);

            pos.X = Math.Max(0, Math.Min(pos.X, CropCanvas.Width));
            pos.Y = Math.Max(0, Math.Min(pos.Y, CropCanvas.Height));

            var rect = new Rect(_dragStart, pos);
            UpdateCropVisuals(rect);

            var tw = (int)rect.Width;
            var th = (int)rect.Height;
            SizeLabel.Text = $"{tw} × {th}";
            Canvas.SetLeft(SizeTooltip, rect.Right + 8);
            Canvas.SetTop(SizeTooltip, rect.Bottom - 20);
            SizeTooltip.Visibility = tw > 10 && th > 10 ? Visibility.Visible : Visibility.Collapsed;

            CropSizeLabel.Text = tw > 0 && th > 0 ? $"Zaznaczenie: {tw} × {th} px" : "";
        }

        private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            CropCanvas.ReleaseMouseCapture();
            SizeTooltip.Visibility = Visibility.Collapsed;

            if (_cropRect.Width > 5 && _cropRect.Height > 5)
            {
                _hasCrop = true;
                BtnCrop.IsEnabled = true;
                StatusLabel.Text = $"Zaznaczono: X={_cropRect.X:0} Y={_cropRect.Y:0}  {_cropRect.Width:0}×{_cropRect.Height:0} px";
            }
            else
            {
                ResetCrop();
            }
        }

        private void UpdateCropVisuals(Rect rect)
        {
            _cropRect = new Rect(
                Math.Min(rect.X, rect.Right),
                Math.Min(rect.Y, rect.Bottom),
                Math.Abs(rect.Width),
                Math.Abs(rect.Height));

            double w = CropCanvas.Width;
            double h = CropCanvas.Height;
            double x = _cropRect.X, y = _cropRect.Y, rw = _cropRect.Width, rh = _cropRect.Height;

            Canvas.SetLeft(CropRect, x);
            Canvas.SetTop(CropRect, y);
            CropRect.Width = rw;
            CropRect.Height = rh;
            CropRect.Visibility = Visibility.Visible;

            SetOverlay(OverlayTop, 0, 0, w, y);
            SetOverlay(OverlayBottom, 0, y + rh, w, h - y - rh);
            SetOverlay(OverlayLeft, 0, y, x, rh);
            SetOverlay(OverlayRight, x + rw, y, w - x - rw, rh);

            UpdateGrid(x, y, rw, rh);

            ShowHandle(HandleTL, x - 5, y - 5);
            ShowHandle(HandleTR, x + rw - 5, y - 5);
            ShowHandle(HandleBL, x - 5, y + rh - 5);
            ShowHandle(HandleBR, x + rw - 5, y + rh - 5);
        }

        private static void SetOverlay(System.Windows.Shapes.Rectangle rect, double x, double y, double w, double h)
        {
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            rect.Width = Math.Max(0, w);
            rect.Height = Math.Max(0, h);
        }

        private void UpdateGrid(double x, double y, double w, double h)
        {
            void SetLine(Line ln, double x1, double y1, double x2, double y2)
            {
                ln.X1 = x1; ln.Y1 = y1; ln.X2 = x2; ln.Y2 = y2;
                ln.Visibility = Visibility.Visible;
            }

            SetLine(GridH1, x, y + h / 3, x + w, y + h / 3);
            SetLine(GridH2, x, y + 2 * h / 3, x + w, y + 2 * h / 3);
            SetLine(GridV1, x + w / 3, y, x + w / 3, y + h);
            SetLine(GridV2, x + 2 * w / 3, y, x + 2 * w / 3, y + h);
        }

        private static void ShowHandle(System.Windows.Shapes.Rectangle handle, double x, double y)
        {
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
            handle.Visibility = Visibility.Visible;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetCrop();

        private void ResetCrop()
        {
            _hasCrop = false;
            _isDragging = false;
            _cropRect = Rect.Empty;

            CropRect.Visibility = Visibility.Collapsed;
            GridH1.Visibility = GridH2.Visibility = GridV1.Visibility = GridV2.Visibility = Visibility.Collapsed;
            HandleTL.Visibility = HandleTR.Visibility = HandleBL.Visibility = HandleBR.Visibility = Visibility.Collapsed;
            SizeTooltip.Visibility = Visibility.Collapsed;

            SetOverlay(OverlayTop, 0, 0, 0, 0);
            SetOverlay(OverlayBottom, 0, 0, 0, 0);
            SetOverlay(OverlayLeft, 0, 0, 0, 0);
            SetOverlay(OverlayRight, 0, 0, 0, 0);

            BtnCrop.IsEnabled = false;
            CropSizeLabel.Text = "";
            if (_originalBitmap != null)
                StatusLabel.Text = "Zaznacz obszar do przycięcia";
        }

        private void BtnCrop_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null || !_hasCrop) return;

            try
            {
                int x = (int)Math.Max(0, _cropRect.X);
                int y = (int)Math.Max(0, _cropRect.Y);
                int w = (int)Math.Min(_cropRect.Width, _originalBitmap.PixelWidth - x);
                int h = (int)Math.Min(_cropRect.Height, _originalBitmap.PixelHeight - y);

                if (w <= 0 || h <= 0)
                {
                    MessageBox.Show("Nieprawidłowy obszar zaznaczenia.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var cropped = new CroppedBitmap(_originalBitmap, new Int32Rect(x, y, w, h));

                string tempDir = Path.GetTempPath();
                string tempFile = Path.Combine(tempDir, $"cropped_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                SaveBitmapAsPng(cropped, tempFile);

                PassToScript(tempFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przycinania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SaveBitmapAsPng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(path, FileMode.Create);
            encoder.Save(fs);
        }

        private void PassToScript(string croppedImagePath)
        {
            string scriptPath = @"script.py"; //script path here

            if (!File.Exists(scriptPath))
            {
                var result = MessageBox.Show(
                    $"Przycięte zdjęcie zapisano do:\n{croppedImagePath}\n\n" +
                    $"Skrypt nie jest jeszcze skonfigurowany.\n");

                StatusLabel.Text = $"Zapisano: {croppedImagePath}";
                return;
            }

            string ext = Path.GetExtension(scriptPath).ToLower();
            ProcessStartInfo psi = ext switch
            {
                ".py" => new ProcessStartInfo("python", $"\"{scriptPath}\" \"{croppedImagePath}\""),
                ".ps1" => new ProcessStartInfo("powershell",
                               $"-ExecutionPolicy Bypass -File \"{scriptPath}\" \"{croppedImagePath}\""),
                ".bat" or ".cmd" => new ProcessStartInfo("cmd",
                               $"/c \"{scriptPath}\" \"{croppedImagePath}\""),
                _ => new ProcessStartInfo(scriptPath, $"\"{croppedImagePath}\""),
            };

            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            try
            {
                using var proc = Process.Start(psi)!;
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30_000);

                string msg = string.IsNullOrWhiteSpace(error)
                    ? $"Skrypt zakończony (kod: {proc.ExitCode})\n\n{output}"
                    : $"Błąd skryptu:\n{error}";

                StatusLabel.Text = $"Skrypt zakończony. Plik: {croppedImagePath}";
                MessageBox.Show(msg, "Wynik skryptu", MessageBoxButton.OK,
                    proc.ExitCode == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się uruchomić skryptu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
