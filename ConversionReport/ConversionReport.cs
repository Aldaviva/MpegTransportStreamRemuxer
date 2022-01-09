using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EntryPoint;

namespace ConversionReport {

    public static class ConversionReport {

        private const double SmallFileSuspicionThreshold = 2.0/3;

        public static void Main(string[] args) {
            var commandLineArguments = Cli.Parse<CommandLineArguments>(args);
            Console.WriteLine($"Scanning for *.wmv files in {commandLineArguments.ParentDirectory}");
            IEnumerable<string> wmvFiles = Directory.EnumerateFiles(commandLineArguments.ParentDirectory, "*.wmv", SearchOption.AllDirectories);

            var reportInfo = new ReportInfo();

            foreach (string wmvFile in wmvFiles) {
                string mp4File = Path.ChangeExtension(wmvFile, "mp4");
                if (!File.Exists(mp4File)) {
                    reportInfo.UnconvertedFiles.Add(wmvFile);
                    reportInfo.RemainingWmvBytesToConvert += new FileInfo(wmvFile).Length;
                } else {
                    long wmvBytes = new FileInfo(wmvFile).Length;
                    long mp4Bytes = new FileInfo(mp4File).Length;
                    reportInfo.WmvBytes += wmvBytes;
                    reportInfo.Mp4Bytes += mp4Bytes;
                    reportInfo.ConvertedFileCount++;

                    if (mp4Bytes * 1.0 / wmvBytes < SmallFileSuspicionThreshold) {
                        reportInfo.SuspiciouslySmallFiles.Add(new SuspiciouslySmallFilePair {
                            WmvFile = wmvFile,
                            Mp4File = mp4File,
                            WmvBytes = wmvBytes,
                            Mp4Bytes = mp4Bytes
                        });
                    }
                }
            }

            if (commandLineArguments.DeleteSuspiciouslySmallFiles) {
                DeleteSuspiciouslySmallFiles(reportInfo.SuspiciouslySmallFiles);
            }

            PrintReport(reportInfo, commandLineArguments.DeleteSuspiciouslySmallFiles);
        }

        private static void DeleteSuspiciouslySmallFiles(IEnumerable<SuspiciouslySmallFilePair> suspiciouslySmallFiles) {
            foreach (SuspiciouslySmallFilePair filePair in suspiciouslySmallFiles) {
                File.Delete(filePair.Mp4File);
            }
        }

        private static void PrintReport(ReportInfo reportInfo, bool deleteSuspiciouslySmallFiles) {
            if (reportInfo.UnconvertedFiles.Count > 0) {
                Console.WriteLine($"{reportInfo.UnconvertedFiles.Count:N0} files were not converted.");
                foreach (string unconvertedFile in reportInfo.UnconvertedFiles) {
                    Console.WriteLine("  - " + unconvertedFile);
                }
            }

            if (reportInfo.UnconvertedFiles.Count > 0 && reportInfo.SuspiciouslySmallFiles.Count > 0) {
                Console.WriteLine();
            }

            if (reportInfo.SuspiciouslySmallFiles.Count > 0) {
                Console.WriteLine(
                    $"{reportInfo.SuspiciouslySmallFiles.Count:N0} files were suspiciously small, and may have been converted incorrectly or incompletely.");
                if (deleteSuspiciouslySmallFiles) {
                    Console.WriteLine("All of the MP4 files for the below WMV files have been deleted.");
                }

                double OrderByCompressionRatio(SuspiciouslySmallFilePair pair) => pair.Mp4Bytes * 1.0 / pair.WmvBytes;
                string OrderByAbsolutePath(SuspiciouslySmallFilePair pair) => pair.WmvFile.ToLowerInvariant();
                foreach (SuspiciouslySmallFilePair filePair in reportInfo.SuspiciouslySmallFiles.OrderBy(OrderByAbsolutePath)) {
                    Console.WriteLine(string.Format(new DataSizeFormatter(), "  - {0} ({1:A1}/{2:A1}, {3:P0})", filePair.WmvFile,
                        filePair.Mp4Bytes, filePair.WmvBytes, filePair.Mp4Bytes * 1.0 / filePair.WmvBytes));
                }
            }

            if (reportInfo.UnconvertedFiles.Count > 0 || reportInfo.SuspiciouslySmallFiles.Count > 0) {
                Console.WriteLine();
            }

            var formatter = new DataSizeFormatter();
            Console.WriteLine(string.Format(formatter, "Converted {0:N0} files from WMV ({1:GB0}) to MP4 ({2:GB0}).",
                reportInfo.ConvertedFileCount, reportInfo.WmvBytes, reportInfo.Mp4Bytes));
            Console.WriteLine(string.Format(formatter, "Using {1:P0} of original size, saving {0:GB0}.",
                reportInfo.WmvBytes - reportInfo.Mp4Bytes,
                reportInfo.Mp4Bytes * 1.0 / reportInfo.WmvBytes));
            Console.WriteLine(string.Format(formatter, "There are {0:N0} WMV files ({1:GB1}) remaining to convert.",
                reportInfo.UnconvertedFiles.Count, reportInfo.RemainingWmvBytesToConvert));
        }

    }

    internal class CommandLineArguments: BaseCliArguments {

        public CommandLineArguments(): base("") { }

        [Operand(1)]
        public string ParentDirectory { get; set; }

        [Option("delete-suspiciously-small-files", 'd')]
        public bool DeleteSuspiciouslySmallFiles { get; set; }

    }

}