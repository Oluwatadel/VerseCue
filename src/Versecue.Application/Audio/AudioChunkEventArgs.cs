namespace Versecue.Infrastructure.Audio
{
    public sealed class AudioChunkEventArgs : EventArgs
    {
        public AudioChunkEventArgs(byte[] audioChunk)
        {
            AudioChunk = audioChunk;
        }

        public byte[] AudioChunk { get; }
    }
}