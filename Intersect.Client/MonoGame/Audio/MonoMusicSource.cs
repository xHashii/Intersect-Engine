using Intersect.Client.Framework.Audio;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Localization;
using Intersect.Client.Utilities;
using Intersect.Logging;

using JetBrains.Annotations;

using Microsoft.Xna.Framework.Audio;

using NVorbis;

using System;

namespace Intersect.Client.MonoGame.Audio
{

    public class MonoMusicSource : GameAudioSource
    {

        private readonly string mPath;

        private VorbisReader mReader;

        private SoundEffectInstance mInstance;

        public MonoMusicSource(string path)
        {
            mPath = path;
        }

        public override GameAudioInstance CreateInstance() => new MonoMusicInstance(this);

        public SoundEffectInstance LoadSong()
        {
            if (string.IsNullOrWhiteSpace(mPath))
            {
                return null;
            }

            try
            {
                if (mReader == null)
                {
                    mReader = new VorbisReader(mPath);
                }

                if (mInstance != null)
                {
                    mInstance.Dispose();
                    mInstance = null;
                }

#if AUDIO_STREAMING
                    var dynamicInstance = new DynamicSoundEffectInstance(mReader.SampleRate, mReader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
                    dynamicInstance.BufferNeeded += BufferNeeded;
                    mInstance = dynamicInstance;
#else
                var soundEffect = Load(mReader);
                mInstance = soundEffect?.CreateInstance();
#endif

                return mInstance;
            }
            catch (Exception exception)
            {
                Log.Error($"Error loading '{mPath}'.", exception);
                ChatboxMsg.AddMessage(
                    new ChatboxMsg(
                        Strings.Errors.LoadFile.ToString(Strings.Words.lcase_sound), new Color(0xBF, 0x0, 0x0)
                    )
                );
            }

            return null;
        }

        public void Close()
        {
            mReader?.Dispose();
            mReader = null;
        }

#if AUDIO_STREAMING
        private void BufferNeeded(object sender, EventArgs args)
            => FillBuffers(mInstance as DynamicSoundEffectInstance);

        private void FillBuffers(DynamicSoundEffectInstance instance, int buffers = 3, int samples = 44100)
        {
            float[] sampleBuffer = null;

            while (instance.PendingBufferCount < buffers && mReader != null)
            {
                if (sampleBuffer == null)
                    sampleBuffer = new float[samples];

                var read = mReader.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                if (read == 0)
                {
                    mReader.DecodedPosition = 0;
                    continue;
                }

                var dataBuffer = new byte[read << 1];
                for (var sampleIndex = 0; sampleIndex < read; ++sampleIndex)
                {
                    var sample = (short)MathHelper.Clamp(sampleBuffer[sampleIndex] * 32767f, short.MinValue, short.MaxValue);
                    var sampleData = BitConverter.GetBytes(sample);
                    for (var sampleByteIndex = 0; sampleByteIndex < sampleData.Length; ++sampleByteIndex)
                        dataBuffer[(sampleIndex << 1) + sampleByteIndex] = sampleData[sampleByteIndex];
                }

                instance.SubmitBuffer(dataBuffer, 0, read << 1);
            }
        }
#else
        private static SoundEffect Load([NotNull] VorbisReader vorbisReader)
        {
            var sampleBuffer = new float[vorbisReader.SampleRate];
            var dataBuffer = new byte[vorbisReader.TotalSamples << 2];
            int samplesRead, dataIndex = 0;
            while (0 != (samplesRead = vorbisReader.ReadSamples(sampleBuffer, 0, vorbisReader.SampleRate)))
            {
                for (var sampleIndex = 0; sampleIndex < samplesRead; ++sampleIndex)
                {
                    var sample = (short)MathHelper.Clamp(sampleBuffer[sampleIndex] * short.MaxValue, short.MinValue, short.MaxValue);
                    var sampleData = BitConverter.GetBytes(sample);
                    for (var sampleDataIndex = 0; sampleDataIndex < sampleData.Length; ++sampleDataIndex, ++dataIndex)
                    {
                        dataBuffer[dataIndex] = sampleData[sampleDataIndex];
                    }
                }
            }

            return new SoundEffect(dataBuffer, vorbisReader.SampleRate,
                vorbisReader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        }
#endif
    }
}
