using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ConversionReport;
using EntryPoint;
using Hudl.FFmpeg;
using Hudl.FFmpeg.Command;
using Hudl.FFmpeg.Command.BaseTypes;
using Hudl.FFmpeg.Enums;
using Hudl.FFmpeg.Exceptions;
using Hudl.FFmpeg.Resources;
using Hudl.FFmpeg.Resources.BaseTypes;
using Hudl.FFmpeg.Settings;
using Hudl.FFmpeg.Settings.BaseTypes;
using Hudl.FFmpeg.Sugar;
using VideoConverter.Hudl;
using PixelFormat = VideoConverter.Hudl.PixelFormat;

namespace VideoConverter {

    public class VideoConverter {

        protected internal const int DEFAULT_CRF = 23;

        private string currentOutputFile;

        public VideoConverter() {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress              += OnProcessExit;
        }

        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<int> Main(string[] args) {
            // BasicConfigurator.Configure(); //uncomment to see ffmpeg.exe command line arguments

            CommandLineArguments commandLineArguments = Cli.Parse<CommandLineArguments>(args);
            if (!string.IsNullOrWhiteSpace(commandLineArguments.parentDirectory)) {
                commandLineArguments.parentDirectory = Path.GetFullPath(commandLineArguments.parentDirectory);
                Console.WriteLine($"Converting .ts, .mp4.part, and .wmv videos in {commandLineArguments.parentDirectory} into .mp4, and " +
                    $"{(commandLineArguments.removeOriginalFilesAfterConverting ? "removing" : "not removing")} original files after conversion.");
                await new VideoConverter().remuxVideos(findFilesToConvert(commandLineArguments.parentDirectory),
                    commandLineArguments.removeOriginalFilesAfterConverting,
                    commandLineArguments.transcodingConstantRateFactor,
                    commandLineArguments.overwriteExistingFiles);
                return 0;
            } else {
                commandLineArguments.OnHelpInvoked(string.Empty);
                // this exits, so the following code is unreachable
                return 1;
            }
        }

        private static IEnumerable<string> findFilesToConvert(string parentDirectory) {
            string[] fileExtensions = { "*.ts", "*.wmv", "*.mp4.part" };
            return fileExtensions.Select(fileExtension => Directory.EnumerateFiles(parentDirectory, fileExtension, SearchOption.AllDirectories))
                .Aggregate((enumerable1, enumerable2) => enumerable1.Concat(enumerable2));
        }

        /// <exception cref="InvalidOperationException"></exception>
        private async Task remuxVideos(IEnumerable<string> inputFiles, bool removeOriginalFilesAfterRemuxing, int? transcodingConstantRateFactor, bool overwriteExistingFiles) {
            ResourceManagement.CommandConfiguration = CommandConfiguration.Create(Path.GetTempPath(), "ffmpeg.exe", "ffprobe.exe");

            IList<JobConfiguration> jobConfigurationsToPost = new List<JobConfiguration>();
            var                     actionBlock             = new ActionBlock<JobConfiguration>(convertToMpeg4Container, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            int                     skippedFileCount        = 0;

            foreach (string inputFile in inputFiles) {
                string fileExtension = Path.GetExtension(inputFile)?.ToLowerInvariant();
                JobConfiguration jobConfiguration = new() {
                    inputFile = inputFile,
                    outputFile = Path.ChangeExtension(inputFile, fileExtension switch {
                        ".part" => null,
                        _       => ".mp4"
                    }),
                    removeInputFileAfterRemux = removeOriginalFilesAfterRemuxing,
                    conversionType = fileExtension switch {
                        ".ts"   => ConversionType.REMUX,
                        ".part" => ConversionType.REMUX,
                        _       => ConversionType.TRANSCODE
                    },
                    transcodingConstantRateFactor = transcodingConstantRateFactor
                };

                if (!File.Exists(jobConfiguration.outputFile) || new FileInfo(jobConfiguration.outputFile).Length == 0 || overwriteExistingFiles) {
                    jobConfigurationsToPost.Add(jobConfiguration);
                } else {
                    Console.WriteLine($"Skipping {jobConfiguration.inputFile}");
                    skippedFileCount++;
                }
            }

            Console.WriteLine($"Skipping {skippedFileCount} existing files.");

            foreach (JobConfiguration jobConfiguration in jobConfigurationsToPost) {
                actionBlock.Post(jobConfiguration);
            }

            actionBlock.Complete();
            await actionBlock.Completion;
        }

        /// <exception cref="InvalidOperationException"></exception>
        private void convertToMpeg4Container(JobConfiguration jobConfiguration) {
            CommandFactory factory = CommandFactory.Create();
            FFmpegCommand  command = factory.CreateOutputCommand();

            CommandStage chain;
            try {
                chain = command.WithInput<VideoStream>(jobConfiguration.inputFile);
            } catch (Exception e) {
                Console.WriteLine($"Skipping file due to failure in pre-conversion analysis of {jobConfiguration.inputFile}: {e.Message}");
                Console.WriteLine(e.StackTrace);
                return;
            }

            SettingsCollection outputSettings = jobConfiguration.conversionType switch {
                ConversionType.TRANSCODE => SettingsCollection.ForOutput(new OverwriteOutput(), new CodecVideo(VideoCodecType.Libx264),
                    new ConstantRateFactor(jobConfiguration.transcodingConstantRateFactor ?? DEFAULT_CRF), new Preset(Preset.Speed.FASTER), new PixelFormat(PixelFormat.Format.YUVJ420P),
                    new CodecAudio(AudioCodecType.ExperimentalAac), new BitRateAudio(128), new MaxMuxingQueueSize(1024)
                    //keep the heat down in the summer
//                        ,new Threads(Math.Min(Environment.ProcessorCount, 2))
//                        ,new Threads(Math.Max(1, Environment.ProcessorCount - 1))
                ),
                ConversionType.REMUX => SettingsCollection.ForOutput(new CodecVideo(VideoCodecType.Copy), new CodecAudio(AudioCodecType.Copy)),
                _                    => throw new InvalidOperationException($"Unknown conversion type {jobConfiguration.conversionType}")
            };

            chain.OnError(OnConversionError);
            chain.To<Mp4>(jobConfiguration.outputFile, outputSettings);

            string conversionPresentParticiple = jobConfiguration.conversionType == ConversionType.REMUX ? "Remuxing" : "Transcoding";
            string inputSize                   = string.Format(new DataSizeFormatter(), "{0:A1}", new FileInfo(jobConfiguration.inputFile).Length);
            Console.WriteLine($"{conversionPresentParticiple} {jobConfiguration.inputFile} ({inputSize})");

            try {
                currentOutputFile = jobConfiguration.outputFile;
                factory.Render();
                currentOutputFile = null;
                if (jobConfiguration.removeInputFileAfterRemux && File.Exists(jobConfiguration.outputFile)) {
                    FileAttributes attributes = File.GetAttributes(jobConfiguration.inputFile);
                    if ((attributes & FileAttributes.ReadOnly) != 0) {
                        File.SetAttributes(jobConfiguration.inputFile, attributes & ~FileAttributes.ReadOnly);
                    }

                    File.Delete(jobConfiguration.inputFile);
                }
            } catch (FFmpegRenderingException e) {
                Console.WriteLine($"Conversion failed for file {jobConfiguration.inputFile}: {e.Message}");
                if (e.InnerException is FFmpegProcessingException e2) {
                    Console.WriteLine(e2.ErrorOutput);
                }

                File.Delete(jobConfiguration.outputFile);
            }
        }

        private static void OnConversionError(ICommandFactory commandFactory, ICommand command, ICommandProcessor results) {
            Console.WriteLine(results.StdOut);
        }

        private void OnProcessExit(object sender, EventArgs eventArgs) {
            Console.WriteLine("\nCleaning up before exiting...");

            Process currentProcess = Process.GetCurrentProcess();
            IEnumerable<Process> childFfmpegProcesses = Process.GetProcesses().Where(process => {
                try {
                    return process.ProcessName == "ffmpeg" && ParentProcessUtilities.getParentProcess(process.Id)?.Id == currentProcess.Id;
                } catch (Win32Exception) {
                    return false;
                }
            });

            foreach (Process childFfmpegProcess in childFfmpegProcesses) {
                Console.WriteLine($"Killing child process {childFfmpegProcess.ProcessName} ({childFfmpegProcess.Id})");
                childFfmpegProcess.Kill();
                childFfmpegProcess.WaitForExit();
            }

            if (currentOutputFile != null) {
                Console.WriteLine($"Deleting incomplete conversion output file {currentOutputFile}");
                File.Delete(currentOutputFile);
            }

            Console.WriteLine("Exiting...");
        }

    }

    internal class JobConfiguration {

        public string inputFile { get; set; }
        public string outputFile { get; set; }
        public bool removeInputFileAfterRemux { get; set; }
        public ConversionType conversionType { get; set; }
        public int? transcodingConstantRateFactor { get; set; }

    }

    internal class CommandLineArguments: BaseCliArguments {

        public CommandLineArguments(): base("") { }

        [Operand(1)]
        public string parentDirectory { get; set; }

        [Option("remove-original-after-converting", 'r')]
        public bool removeOriginalFilesAfterConverting { get; set; }

        [Option("overwrite-existing", 'o')]
        public bool overwriteExistingFiles { get; set; }

        [OptionParameter("crf")]
        public int? transcodingConstantRateFactor { get; set; }

        public override void OnHelpInvoked(string helpText) {
            string selfExecutableFilename = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine($@"Usage:

    {selfExecutableFilename} [-r] <path>

Example:

    {selfExecutableFilename} -r --crf 23 ""D:\Play\Video""

Description:

    Scan a directory recursively for MPEG transport stream video files (.ts), Windows Media Video files (.wmv), and incomplete
    downloads from youtube-dl, e.g. ChaturbateStreamDownloader (.mp4.part).
    Any such files will be converted into an MP4 container.
    The original files can be optionally deleted after the conversion is complete.

Arguments:

    -r, --remove-original-after-converting    After a file is converted into MP4, delete the original file. Defaults to off.
    -o, --overwrite-existing                  Instead of skipping WMV files that have already been converted to MP4, convert them again, overwriting the existing MP4 files.
    --crf {VideoConverter.DEFAULT_CRF}                                  When transcoding WMV, the x264 Constant Rate Factor to use (0-51, 18-28, 23 normal, {VideoConverter.DEFAULT_CRF} default, higher is lossier)
    path                                      Directory to scan for video files. Subdirectories will also be scanned.
");
            Environment.Exit(1);
        }

    }

    internal enum ConversionType {

        REMUX,
        TRANSCODE

    }

}