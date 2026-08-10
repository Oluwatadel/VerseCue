namespace Versecue.Application.Audio
{
    public class TranscriptReceivedEventArgs : EventArgs
    {
        public TranscriptReceivedEventArgs(
        string text,
        TimeSpan startTime,
        TimeSpan endTime)
        {
            Text = text;
            StartTime = startTime;
            EndTime = endTime;
        }

        public string Text { get; }

        public TimeSpan StartTime { get; }

        public TimeSpan EndTime { get; }
    }
}
