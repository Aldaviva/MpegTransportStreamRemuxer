using Hudl.FFmpeg.Attributes;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Interfaces;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings.Interfaces;
using Hudl.FFmpeg.Settings.Attributes;

namespace VideoConverter.Hudl {

    [ForStream(Type = typeof(VideoStream))]
    [Setting(Name = "preset", ResourceType = SettingsCollectionResourceType.Output)]
    public class Preset: ISetting {

        public Preset(Speed value) {
            this.value = value;
        }

        [SettingParameter(Formatter = typeof(PresetFormatter))]
        public Speed value { get; }

        /// <summary>
        /// <c>ffmpeg -hide_banner -f lavfi -i nullsrc -c:v libx264 -preset help -f mp4 NUL</c>
        /// </summary>
        public enum Speed {

            ULTRAFAST,
            SUPERFAST,
            VERYFAST,
            FASTER,
            FAST,
            MEDIUM,
            SLOW,
            SLOWER,
            VERYSLOW,
            PLACEBO

        }

        private class PresetFormatter: IFormatter {

            public string Format(object value) {
                return value.ToString().ToLowerInvariant();
            }

        }

    }

}