using Hudl.FFmpeg.Attributes;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings.Attributes;
using Hudl.FFmpeg.Settings.Interfaces;

namespace VideoConverter.Hudl {

    /// <summary>Useful when input files have lots of packets for one stream before the other stream gets its first packet.
    /// <para>Without this option, FFmpeg will buffer 128 packets as it tries to find the first packet from each frame. If it can't find the first packet of one of the streams before the buffer fills, the job will fail with the error message "Too many packets buffered for output stream."</para></summary>
    /// <remarks>See <a href="https://trac.ffmpeg.org/ticket/6375">FFmpeg #6375</a></remarks>
    [ForStream(Type = typeof(AudioStream))]
    [ForStream(Type = typeof(VideoStream))]
    [Setting(Name = "max_muxing_queue_size", ResourceType = SettingsCollectionResourceType.Output)]
    public class MaxMuxingQueueSize: ISetting {

        [SettingParameter]
        public int queueSizePackets { get; }

        public MaxMuxingQueueSize(int queueSizePackets) {
            this.queueSizePackets = queueSizePackets;
        }

    }

}