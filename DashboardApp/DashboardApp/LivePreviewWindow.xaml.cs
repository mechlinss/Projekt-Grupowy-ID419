using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DashboardApp
{
    public record ParameterDef(
        string Name,
        string DisplayName,
        double Min,
        double Max,
        double Default,
        double Step        = 1,
        bool   SnapToTick  = false);

    public record ScriptParams(string[] Values)
    {
        public string ToArgString() => string.Join(" ", Values);
    }

    public partial class LivePreviewWindow : Window
    {
        private readonly string                      _inputImagePath;
        private readonly string                      _scriptPath;
        private readonly string                      _pythonExe;
        private readonly List<ParameterDef>          _paramDefs;
        private readonly List<(ParameterDef, Slider, TextBlock)> _controls = new();

        public event Action<ScriptParams>? AnalysisConfirmed;

        public LivePreviewWindow(
            string inputImagePath,
            string scriptPath,
            string pythonExe,
            string scriptDisplayName,
            List<ParameterDef> paramDefs)
        {
            InitializeComponent();
            _inputImagePath  = inputImagePath;
            _scriptPath      = scriptPath;
            _pythonExe       = pythonExe;
            _paramDefs       = paramDefs;

            ScriptNameLabel.Text = scriptDisplayName;
            FileNameLabel.Text   = Path.GetFileName(inputImagePath);

            BuildParamControls();
            _ = RunAnalysis();
        }


        private void BuildParamControls()
        {
            ParamsPanel.Children.Clear();
            _controls.Clear();

            foreach (var def in _paramDefs)
            {
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock
                {
                    Text       = def.DisplayName,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 12,
                    Margin     = new Thickness(0, 12, 0, 2),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                Grid.SetColumn(label, 0);

                var valueLabel = new TextBlock
                {
                    Foreground  = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    FontFamily  = new FontFamily("Consolas"),
                    FontSize    = 13,
                    FontWeight  = FontWeights.SemiBold,
                    MinWidth    = 40,
                    TextAlignment = TextAlignment.Right,
                    Margin      = new Thickness(0, 12, 0, 2),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                Grid.SetColumn(valueLabel, 1);

                headerGrid.Children.Add(label);
                headerGrid.Children.Add(valueLabel);
                ParamsPanel.Children.Add(headerGrid);

                var slider = new Slider
                {
                    Minimum          = def.Min,
                    Maximum          = def.Max,
                    Value            = def.Default,
                    SmallChange      = def.Step,
                    LargeChange      = def.Step * 5,
                    IsSnapToTickEnabled = def.SnapToTick,
                    TickFrequency    = def.Step,
                    Foreground       = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    IsMoveToPointEnabled = true,
                    Margin           = new Thickness(0, 2, 0, 0)
                };

                UpdateValueLabel(valueLabel, slider.Value, def);

                slider.ValueChanged += (_, e) =>
                {
                    if (!def.SnapToTick && def.Step > 1)
                    {
                        double snapped = Math.Round(e.NewValue / def.Step) * def.Step;
                        if (Math.Abs(slider.Value - snapped) > 0.001)
                            slider.Value = snapped;
                    }
                    UpdateValueLabel(valueLabel, slider.Value, def);
                };

                ParamsPanel.Children.Add(slider);
                _controls.Add((def, slider, valueLabel));
            }
        }

        private static void UpdateValueLabel(TextBlock tb, double value, ParameterDef def)
        {
            tb.Text = def.Step >= 1 ? ((int)Math.Round(value)).ToString()
                                    : value.ToString("F2");
        }


        public ScriptParams CurrentParams
        {
            get
            {
                var values = _controls.Select(t =>
                {
                    double v = t.Item2.Value;
                    return t.Item1.Step >= 1 ? ((int)Math.Round(v)).ToString() : v.ToString("F2");
                }).ToArray();
                return new ScriptParams(values);
            }
        }


        private async void BtnApply_Click(object sender, RoutedEventArgs e)   => await RunAnalysis();
        private void       BtnClose_Click(object sender, RoutedEventArgs e)   => Close();

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            AnalysisConfirmed?.Invoke(CurrentParams);
            Close();
        }


        private async Task RunAnalysis()
        {
            BtnApply.IsEnabled        = false;
            BtnConfirm.IsEnabled      = false;
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                string tempOutput = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid()}.png");
                string extraArgs  = _paramDefs.Count > 0 ? " " + CurrentParams.ToArgString() : "";
                string args       = $"\"{_scriptPath}\" \"{_inputImagePath}\" \"{tempOutput}\"{extraArgs}";

                var psi = new ProcessStartInfo(_pythonExe, args)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                using var proc = Process.Start(psi)!;
                string output  = await proc.StandardOutput.ReadToEndAsync();
                string error   = await proc.StandardError.ReadToEndAsync();
                proc.WaitForExit(30_000);

                if (proc.ExitCode != 0 || !File.Exists(tempOutput))
                {
                    ResultLabel.Text       = "Błąd!";
                    ResultLabel.Foreground = Brushes.Red;
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource     = new Uri(tempOutput);
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                bmp.Freeze();
                PreviewImage.Source = bmp;

                try
                {
                    var doc   = System.Text.Json.JsonDocument.Parse(output.Trim());
                    int count = doc.RootElement.GetProperty("Ilosc krysztalow").GetInt32();
                    ResultLabel.Text       = $"{count} kryształów";
                    ResultLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                }
                catch
                {
                    ResultLabel.Text = output.Trim();
                }
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnApply.IsEnabled        = true;
                BtnConfirm.IsEnabled      = true;
            }
        }
    }
}
