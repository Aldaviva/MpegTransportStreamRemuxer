using Hudl.FFmpeg.Attributes;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Resources.Interfaces;

namespace VideoConverter.Hudl {

    /// Represents an <c>.mp4.part</c> file, otherwise FFmpegCommand.WithInput() will fail with an InvalidOperationException: Cannot derive resource type from path provided.
    [ContainsStream(Type = typeof(AudioStream))]
    [ContainsStream(Type = typeof(VideoStream))]
    [ContainsStream(Type = typeof(DataStream))]
    public class Part: BaseContainer {

        public Part(): base(".part") { }

        protected override IContainer Clone() {
            return new Part();
        }

    }

}