using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Versecue.Infrastructure.Audio
{
    public sealed class AudioOptions
    {
        public int SampleRate { get; init; } = AudioConstants.SampleRate;

        public int Channels { get; init; } = AudioConstants.Channels;

        public int BitsPerSample { get; init; } = AudioConstants.BitsPerSample;

        public int BufferMilliseconds { get; init; } =
            AudioConstants.BufferMilliseconds;
    }
}
