using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.ComponentModel.Composition;
using Contracts;

namespace TextWidget
{
    [Export(typeof(IWidget))]
    [ExportMetadata("Name", "Analizator Tekstu")]
    [ExportMetadata("DefaultLocation", "Right")]
    public partial class TextWidget : UserControl, IWidget
    {
        public new string Name => "Analizator Tekstu";
        public object View => this;

        [ImportingConstructor]
        public TextWidget(IEventAggregator aggregator)
        {
            InitializeComponent();
            aggregator.Subscribe<DataSubmittedEvent>(OnData);
        }

        private void OnData(DataSubmittedEvent e)
        {
            var text = e.Text ?? "";

            CharCount.Text = $"Znaki: {text.Length}";
            WordCount.Text = $"Słowa: {text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length}";
        }
    }
}
