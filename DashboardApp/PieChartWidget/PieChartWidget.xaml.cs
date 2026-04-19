using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.ComponentModel.Composition;
using Contracts;

namespace PieChartWidget
{
    [Export(typeof(IWidget))]
    [ExportMetadata("Name", "Wykres Kołowy")]
    [ExportMetadata("DefaultLocation", "Document")]
    public partial class PieChartWidget : UserControl, IWidget
    {
        public new string Name => "Wykres Kołowy";
        public object View => this;

        [ImportingConstructor]
        public PieChartWidget(IEventAggregator aggregator)
        {
            InitializeComponent();
            aggregator.Subscribe<DataSubmittedEvent>(OnData);
        }

        private void OnData(DataSubmittedEvent e)
        {
            PieCanvas.Children.Clear();

            if (string.IsNullOrWhiteSpace(e.Text))
                return;

            var numbers = e.Text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();

            if (numbers.Count == 0)
                return;

            double total = numbers.Sum();
            double startAngle = 0;
            double radius = 120;
            double centerX = 150;
            double centerY = 150;

            Random rnd = new Random();

            foreach (var value in numbers)
            {
                double sweepAngle = value / total * 360;

                var slice = CreateSlice(startAngle, sweepAngle, radius, centerX, centerY, rnd);
                PieCanvas.Children.Add(slice);

                startAngle += sweepAngle;
            }
        }

        private Path CreateSlice(double startAngle, double sweepAngle, double radius, double centerX, double centerY, Random rnd)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            var startPoint = new System.Windows.Point(
                centerX + radius * Math.Cos(startRad),
                centerY + radius * Math.Sin(startRad));

            var endPoint = new System.Windows.Point(
                centerX + radius * Math.Cos(endRad),
                centerY + radius * Math.Sin(endRad));

            bool isLargeArc = sweepAngle > 180;

            var figure = new PathFigure
            {
                StartPoint = new System.Windows.Point(centerX, centerY),
                Segments =
                {
                    new LineSegment(startPoint, true),
                    new ArcSegment(endPoint, new System.Windows.Size(radius, radius),
                                   0, isLargeArc, SweepDirection.Clockwise, true)
                },
                IsClosed = true
            };

            return new Path
            {
                Fill = new SolidColorBrush(Color.FromRgb(
                    (byte)rnd.Next(50, 200),
                    (byte)rnd.Next(50, 200),
                    (byte)rnd.Next(50, 200))),
                Data = new PathGeometry { Figures = { figure } }
            };
        }
    }
}
