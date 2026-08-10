using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versecue.Application.Audio
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
