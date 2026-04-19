using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.ComponentModel.Composition;
using Contracts;

namespace ChartsWidget
{
    [Export(typeof(IWidget))]
    [ExportMetadata("Name", "Wykres Liczb")]
    [ExportMetadata("DefaultLocation", "Right")]
    public partial class ChartsWidget : UserControl, IWidget
    {
        public new string Name => "Wykres Liczb";
        public object View => this;

        [ImportingConstructor]
        public ChartsWidget(IEventAggregator aggregator)
        {
            InitializeComponent();
            aggregator.Subscribe<DataSubmittedEvent>(OnData);
        }

        private void OnData(DataSubmittedEvent e)
        {
            Bars.Children.Clear();

            if (string.IsNullOrWhiteSpace(e.Text))
                return;

            var numbers = e.Text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .ToList();

            foreach (var n in numbers)
            {
                Bars.Children.Add(new Rectangle
                {
                    Width = 25,
                    Height = n * 2, // skala wysokości
                    Fill = Brushes.SteelBlue,
                    Margin = new System.Windows.Thickness(5, 0, 5, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom
                });
            }
        }
    }
}
