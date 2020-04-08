using System;
using System.Threading.Tasks;

using Intersect.Client.General;

using JetBrains.Annotations;

using Microsoft.Xna.Framework.Audio;

namespace Intersect.Client.MonoGame.Audio
{

    public class MonoMusicInstance : MonoAudioInstance<MonoMusicSource>
    {

        public static MonoMusicInstance Instance = null;

        private SoundEffectInstance mSong;

        private bool mLoading;

        private AudioInstanceState mDelayedAudioInstanceState;

        private readonly MonoMusicSource mSource;

        private bool mDisposed;

        private int mVolume;

        // ReSharper disable once SuggestBaseTypeForParameter
        public MonoMusicInstance([NotNull] MonoMusicSource source) : base(source)
        {
            // Only allow one music player at a time
            Instance?.Stop();
            Instance?.Dispose();
            Instance = this;

            mSource = source;
            mDelayedAudioInstanceState = AudioInstanceState.Stopped;
            mLoading = true;

            Task.Run(
                async () =>
                {
                    mSong = await source.LoadSong();
                    mLoading = false;

                    InternalLoopSet();
#if !AUDIO_STREAMING
                    SynchronizeVolume();
#endif

                    switch (mDelayedAudioInstanceState)
                    {
                        case AudioInstanceState.Playing:
                            mSong?.Play();
                            break;

                        case AudioInstanceState.Paused:
                            mSong?.Pause();
                            break;

                        case AudioInstanceState.Stopped:
                            // Nothing really to do, it should already be stopped
                            break;

                        case AudioInstanceState.Disposed:
                            // Definitely nothing to do, though it shouldn't be possible
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(mDelayedAudioInstanceState),
                                $@"{(int) mDelayedAudioInstanceState} is not a valid value for {nameof(AudioInstanceState)}."
                            );
                    }
                }
            );
        }

        public override AudioInstanceState State
        {
            get
            {
                if (mSong == null || mSong.IsDisposed)
                {
                    return AudioInstanceState.Disposed;
                }

                switch (mSong.State)
                {
                    case SoundState.Playing:
                        return AudioInstanceState.Playing;

                    case SoundState.Paused:
                        return AudioInstanceState.Paused;

                    case SoundState.Stopped:
                        return AudioInstanceState.Stopped;

                    default:
                        return AudioInstanceState.Disposed;
                }
            }
        }

        public override void Play()
        {
            mDelayedAudioInstanceState = AudioInstanceState.Playing;
            if (mSong != null && !mSong.IsDisposed)
            {
                mSong.Play();
            }
        }

        public override void Pause()
        {
            mDelayedAudioInstanceState = AudioInstanceState.Paused;
            if (mSong != null && !mSong.IsDisposed)
            {
                mSong.Pause();
            }
        }

        public override void Stop()
        {
            mDelayedAudioInstanceState = AudioInstanceState.Stopped;
            if (mSong != null && !mSong.IsDisposed)
            {
                mSong.Stop();
            }
        }

        public override void SetVolume(int volume, bool isMusic = false)
        {
            mVolume = volume;

            if (mSong == null || mSong.IsDisposed)
            {
                return;
            }

            SynchronizeVolume();
        }

        private void SynchronizeVolume()
        {
            if (mSong == null)
            {
                return;
            }

            try
            {
                mSong.Volume = ComputeVolume(mVolume, Globals.Database.MusicVolume);
            }
            catch (NullReferenceException)
            {
                // song changed while changing volume
            }
            catch (Exception)
            {
                // device not ready
            }
        }

        public override int GetVolume()
        {
            return mVolume;
        }

        protected override void InternalLoopSet()
        {
            if (mSong != null)
            {
#if !AUDIO_STREAMING
                mSong.IsLooped = IsLooping;
#endif
            }
        }

        public override void Dispose()
        {
            mDisposed = true;
            try
            {
                if (mSong != null && !mSong.IsDisposed)
                {
                    mSong.Stop();
                    mSong.Dispose();
                }

                mSource.Close();
            }
            catch
            {
                /* This is just to catch any B.S. errors that MonoGame shouldn't be throwing to us. */
            }
        }

        ~MonoMusicInstance()
        {
            mDisposed = true;

            //We don't want to call MediaPlayer.Stop() here because it's static and another song has likely started playing.
        }

    }

}
