using System.Windows.Controls;

namespace TextWidget
{
    public partial class TextWidgetView : UserControl
    {
        public TextWidgetView()
        {
            InitializeComponent();
        }

        public void UpdateText(string text)
        {
            DisplayText.Text = text;
        }
    }
}
