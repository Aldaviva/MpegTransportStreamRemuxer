using System.Diagnostics.CodeAnalysis;
using Hudl.FFmpeg.Attributes;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings.Attributes;

namespace VideoConverter.Hudl {

    [ForStream(Type = typeof(VideoStream))]
    [Setting(Name   = "pix_fmt")]
    public class PixelFormat: global::Hudl.FFmpeg.Settings.PixelFormat {

        public PixelFormat(Format format): base(format.ToString().ToLowerInvariant()) { }

        /// <summary>
        ///     <c>ffmpeg -h encoder=libx264</c>
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Format {

            YUV420P,
            YUVJ420P,
            YUV422P,
            YUVJ422P,
            YUV444P,
            YUVJ444P,
            NV12,
            NV16,
            NV21,
            YUV420P10LE,
            YUV422P10LE,
            YUV444P10LE,
            NV20LE,
            GRAY,
            GRAY10LE

        }

    }

}