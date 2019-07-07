using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Hudl.FFmpeg;
using Hudl.FFmpeg.Command;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Resources;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings;
using Hudl.FFmpeg.Settings.BaseTypes;
using Hudl.FFmpeg.Sugar;

namespace MpegTransportStreamRemuxer {

    internal class Program {

        private static async Task Main() {
            await new Program().RemuxVideos(FindTransportStreamFiles(@"D:\Play\Video\"));
        }

        public static IEnumerable<string> FindTransportStreamFiles(string parentDirectory) {
            Console.WriteLine($"Finding *.ts files in {parentDirectory}");
            return Directory.EnumerateFiles(parentDirectory, "*.ts", SearchOption.AllDirectories);
        }

        public async Task RemuxVideos(IEnumerable<string> inputFiles) {
            ResourceManagement.CommandConfiguration = CommandConfiguration.Create(Path.GetTempPath(), "ffmpeg.exe", "ffprobe.exe");

            var actionBlock = new ActionBlock<JobConfiguration>(RemuxToMpeg4Container,
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

            foreach (string inputFile in inputFiles) {
                var jobConfiguration = new JobConfiguration {
                    InputFile = inputFile,
                    OutputFile = Path.ChangeExtension(inputFile, "mp4")
                };

                if (!File.Exists(jobConfiguration.OutputFile)) {
                    actionBlock.Post(jobConfiguration);
                } else {
                    Console.WriteLine($"Skipping existing file {jobConfiguration.OutputFile}");
                }
            }

            actionBlock.Complete();
            await actionBlock.Completion;
        }

        private void RemuxToMpeg4Container(JobConfiguration jobConfiguration) {
            CommandFactory factory = CommandFactory.Create();
            FFmpegCommand command = factory.CreateOutputCommand();

            CommandStage chain = command.WithInput<VideoStream>(jobConfiguration.InputFile);

            SettingsCollection outputSettings = SettingsCollection.ForOutput(new CodecVideo(VideoCodecType.Copy),
                new CodecAudio(AudioCodecType.Copy));

            chain.To<Mp4>(jobConfiguration.OutputFile, outputSettings);

            Console.WriteLine($"Remuxing {jobConfiguration.InputFile}");
            factory.Render();
            Console.WriteLine($"Remuxed  {jobConfiguration.OutputFile}");
        }

    }

    internal class JobConfiguration {

        public string InputFile { get; set; }
        public string OutputFile { get; set; }

    }

}