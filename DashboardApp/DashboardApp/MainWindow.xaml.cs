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

        private List<ScriptEntry> _scripts = new();

        private void LoadScripts()
        {
            string jsonPath = Path.Combine(ProjectRoot, @"scripts\scripts.json");
            if (!File.Exists(jsonPath))
            {
                StatusLabel.Text = $"Nie znaleziono pliku scripts.json: {jsonPath}";
                return;
            }

            try
            {
                var doc  = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                var list = doc.RootElement.GetProperty("scripts");

                foreach (var s in list.EnumerateArray())
                {
                    var paramDefs = new List<ParameterDef>();
                    if (s.TryGetProperty("params", out var paramsEl))
                    {
                        foreach (var p in paramsEl.EnumerateArray())
                        {
                            paramDefs.Add(new ParameterDef(
                                Name:        p.GetProperty("name").GetString()!,
                                DisplayName: p.GetProperty("displayName").GetString()!,
                                Min:         p.GetProperty("min").GetDouble(),
                                Max:         p.GetProperty("max").GetDouble(),
                                Default:     p.GetProperty("default").GetDouble(),
                                Step:        p.TryGetProperty("step", out var st)  ? st.GetDouble()  : 1,
                                SnapToTick:  p.TryGetProperty("snapToTick", out var sn) && sn.GetBoolean()
                            ));
                        }
                    }

                    _scripts.Add(new ScriptEntry(
                        DisplayName:  s.GetProperty("displayName").GetString()!,
                        RelativePath: s.GetProperty("relativePath").GetString()!,
                        Params:       paramDefs
                    ));
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Błąd wczytywania scripts.json: {ex.Message}";
            }
        }

        private string ProjectRoot =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\"));

        private string VenvPython =>
            Path.Combine(ProjectRoot, @"scripts\venv\Scripts\python.exe");

        private string? _systemPython = null;

        private string PythonExe =>
            File.Exists(VenvPython) ? VenvPython : (_systemPython ?? "py");

        public MainWindow()
        {
            InitializeComponent();
            LoadScripts();
            PopulateScriptSelector();
            Loaded += async (_, _) => await EnsureVenvAsync();
        }


        private static readonly string[] PythonCandidates = { "py", "python", "python3" };

        private static async Task<string?> FindSystemPythonAsync()
        {
            foreach (var candidate in PythonCandidates)
            {
                try
                {
                    var psi = new ProcessStartInfo(candidate, "--version")
                    {
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0) return candidate;
                }
                catch {  }
            }
            return null;
        }

        private async Task EnsureVenvAsync()
        {
            if (File.Exists(VenvPython)) return;

            BtnCrop.IsEnabled = false;
            BtnLoad.IsEnabled = false;
            StatusLabel.Text  = "Szukam Pythona...";

            _systemPython = await FindSystemPythonAsync();

            if (_systemPython == null)
            {
                StatusLabel.Text = "Nie znaleziono Pythona (py / python / python3). Zainstaluj Python i uruchom ponownie.";
                return;
            }

            StatusLabel.Text = $"Tworzę środowisko wirtualne ({_systemPython})...";

            string scriptsDir       = Path.Combine(ProjectRoot, "scripts");
            string requirementsPath = Path.Combine(scriptsDir, "requirements.txt");

            bool ok = await RunCmdAsync(_systemPython, $"-m venv \"{Path.Combine(scriptsDir, "venv")}\"");
            if (!ok)
            {
                StatusLabel.Text = "Nie udało się utworzyć venv.";
                BtnLoad.IsEnabled = true;
                return;
            }

            StatusLabel.Text = "Instaluję zależności (opencv, numpy, matplotlib)...";

            string venvPip = Path.Combine(scriptsDir, @"venv\Scripts\pip.exe");
            ok = await RunCmdAsync(venvPip, $"install -r \"{requirementsPath}\" --quiet");
            if (!ok)
            {
                StatusLabel.Text = "Błąd instalacji pakietów. Sprawdź plik requirements.txt.";
                BtnLoad.IsEnabled = true;
                return;
            }

            StatusLabel.Text  = "Środowisko gotowe.";
            BtnLoad.IsEnabled = true;
            BtnCrop.IsEnabled = _loadedImages.Count > 0;
        }

        private static async Task<bool> RunCmdAsync(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };
                using var proc = Process.Start(psi)!;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
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
