using Intersect.Client.Framework.Audio;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Localization;
using Intersect.Logging;

using JetBrains.Annotations;

using Microsoft.Xna.Framework.Audio;

using NVorbis;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

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

        public async Task<SoundEffectInstance> LoadSong()
        {
            if (string.IsNullOrWhiteSpace(mPath))
            {
                return null;
            }

            return await Task.Run(
                () =>
                {
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
                });
        }

        public void Close()
        {
            mReader?.Dispose();
            mReader = null;
        }

#if AUDIO_STREAMING
        private void BufferNeeded(object sender, EventArgs args)
            => FillBuffers(mInstance as DynamicSoundEffectInstance ?? throw new InvalidOperationException());

        private void FillBuffers([NotNull] DynamicSoundEffectInstance dynamicInstance, int buffers = 3, int samples = 44100)
        {
            var sampleBuffer = new float[samples];
            var dataBuffer = new byte[samples << 1];

            while (dynamicInstance.PendingBufferCount < buffers && mReader != null)
            {
                var samplesRead = mReader.ReadSamples(sampleBuffer, 0, sampleBuffer.Length);
                if (samplesRead == 0)
                {
                    mReader.DecodedPosition = 0;
                    continue;
                }

                var dataIndex = 0;

                for (var sampleIndex = 0; sampleIndex < samplesRead; ++sampleIndex)
                {
                    var sample = (short)(sampleBuffer[sampleIndex] * short.MaxValue);
                    dataBuffer[dataIndex++] = (byte)sample;
                    dataBuffer[dataIndex++] = (byte)(sample >> 8);
                }

                dynamicInstance.SubmitBuffer(dataBuffer, 0, samplesRead << 1);
            }
        }
#else
        private static SoundEffect Load([NotNull] VorbisReader vorbisReader)
        {
            var sampleRate = vorbisReader.SampleRate;
            var sampleBuffer = new float[sampleRate];
            var dataBuffer = new byte[vorbisReader.TotalSamples << 2];
            int samplesRead, dataIndex = 0;

            var stopwatch = Stopwatch.StartNew();
            while (0 != (samplesRead = vorbisReader.ReadSamples(sampleBuffer, 0, sampleRate)))
            {
                for (var sampleIndex = 0; sampleIndex < samplesRead; ++sampleIndex)
                {
                    var sample = (short)(sampleBuffer[sampleIndex] * short.MaxValue);
                    dataBuffer[dataIndex++] = (byte) sample;
                    dataBuffer[dataIndex++] = (byte) (sample >> 8);
                }
            }

            stopwatch.Stop();

            Debug.WriteLine($"Loading took {stopwatch.ElapsedMilliseconds / 1000f}s.");

            // var internalBuffer = new MemoryStream();
            // var buffer = new GZipStream(internalBuffer, CompressionLevel.Optimal);
            // buffer.Write(dataBuffer, 0, dataBuffer.Length);

            return new SoundEffect(dataBuffer, vorbisReader.SampleRate,
                vorbisReader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        }
#endif
    }
}
