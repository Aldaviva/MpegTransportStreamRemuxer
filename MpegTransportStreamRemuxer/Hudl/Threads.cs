using Hudl.FFmpeg.Attributes;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings.Attributes;
using Hudl.FFmpeg.Settings.Interfaces;

namespace VideoConverter.Hudl {

    /// <summary>
    /// Change the maximum number of threads to use for encoding. Default is 0, which means auto-detect based on the number of CPU cores.
    /// </summary>
    [ForStream(Type = typeof(AudioStream))]
    [ForStream(Type = typeof(VideoStream))]
    [Setting(Name = "threads", ResourceType = SettingsCollectionResourceType.Output)]
    public class Threads: ISetting {

        [SettingParameter]
        public int maxThreads { get; }

        public Threads(int maxThreads) {
            this.maxThreads = maxThreads;
        }

    }

}