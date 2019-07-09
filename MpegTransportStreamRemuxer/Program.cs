using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EntryPoint;
using EntryPoint.Help;
using Hudl.FFmpeg;
using Hudl.FFmpeg.Command;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Exceptions;
using Hudl.FFmpeg.Resources;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings;
using Hudl.FFmpeg.Settings.BaseTypes;
using Hudl.FFmpeg.Sugar;

namespace MpegTransportStreamRemuxer {

    public class Program {

        public static async Task<int> Main(string[] args) {
            var commandLineArguments = Cli.Parse<CommandLineArguments>(args);
            if (!string.IsNullOrWhiteSpace(commandLineArguments.ParentDirectory)) {
                Console.WriteLine($"Remuxing .ts videos in {commandLineArguments.ParentDirectory} into .mp4, " +
                                  $"and {(commandLineArguments.RemoveTransportStreamFilesAfterRemuxing ? "removing" : "not removing")} .ts files after remuxing");
                await RemuxVideos(FindTransportStreamFiles(commandLineArguments.ParentDirectory),
                    commandLineArguments.RemoveTransportStreamFilesAfterRemuxing);
                return 0;
            } else {
                string selfExecutableFilename = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                Console.WriteLine($@"Usage:

    {selfExecutableFilename} [-r] <path>

Example:

    {selfExecutableFilename} -r ""D:\Play\Video""

Description:

    Scan a directory recursively for MPEG transport stream video files (.ts).
    Any such files will be losslessly converted into an MP4 container (remuxed).
    The original .ts files can be optionally deleted after the remuxing is complete.

Arguments:

    -r, --remove-ts-after-remuxing    After a .ts file is remuxed into .mp4, delete the .ts file. Defaults to off.
    path                              Directory to scan for .ts files. Subdirectories will also be scanned.
");
                return 1;
            }
        }

        public static IEnumerable<string> FindTransportStreamFiles(string parentDirectory) {
            return Directory.EnumerateFiles(parentDirectory, "*.ts", SearchOption.AllDirectories);
        }

        public static async Task RemuxVideos(IEnumerable<string> inputFiles, bool removeTransportStreamFilesAfterRemuxing) {
            ResourceManagement.CommandConfiguration = CommandConfiguration.Create(Path.GetTempPath(), "ffmpeg.exe", "ffprobe.exe");

            var actionBlock = new ActionBlock<JobConfiguration>(RemuxToMpeg4Container,
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

            foreach (string inputFile in inputFiles) {
                var jobConfiguration = new JobConfiguration {
                    InputFile = inputFile,
                    OutputFile = Path.ChangeExtension(inputFile, "mp4"),
                    RemoveInputFileAfterRemux = removeTransportStreamFilesAfterRemuxing
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

        private static void RemuxToMpeg4Container(JobConfiguration jobConfiguration) {
            CommandFactory factory = CommandFactory.Create();
            FFmpegCommand command = factory.CreateOutputCommand();

            CommandStage chain = command.WithInput<VideoStream>(jobConfiguration.InputFile);

            SettingsCollection outputSettings = SettingsCollection.ForOutput(new CodecVideo(VideoCodecType.Copy),
                new CodecAudio(AudioCodecType.Copy));

            chain.To<Mp4>(jobConfiguration.OutputFile, outputSettings);

            Console.WriteLine($"Remuxing {jobConfiguration.InputFile}");
            try {
                factory.Render();
//                Console.WriteLine($"Remuxed  {jobConfiguration.OutputFile}");
                if (jobConfiguration.RemoveInputFileAfterRemux && File.Exists(jobConfiguration.OutputFile)) {
                    File.Delete(jobConfiguration.InputFile);
                }
            } catch (FFmpegRenderingException e) {
                Console.WriteLine($"Remuxing failed for file {jobConfiguration.InputFile}: {e.Message}");
            }
        }

    }

    internal class JobConfiguration {

        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public bool RemoveInputFileAfterRemux { get; set; }

    }

    internal class CommandLineArguments: BaseCliArguments {

        public CommandLineArguments(): base("") { }

        [Operand(1)]
        public string ParentDirectory { get; set; }

        [Option("remove-ts-after-remuxing", 'r')]
        public bool RemoveTransportStreamFilesAfterRemuxing { get; set; }

    }

}