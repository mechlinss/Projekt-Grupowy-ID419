namespace Contracts
{
    public class DataSubmittedEvent
    {
        public string Text { get; }

        public DataSubmittedEvent(string text)
        {
            Text = text;
        }
    }
}
