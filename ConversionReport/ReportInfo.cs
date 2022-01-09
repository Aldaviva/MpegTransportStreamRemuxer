using System.Collections.Generic;

namespace ConversionReport {

    public class ReportInfo {

        public readonly IList<string> UnconvertedFiles = new List<string>();
        public readonly IList<SuspiciouslySmallFilePair> SuspiciouslySmallFiles = new List<SuspiciouslySmallFilePair>();
        public long WmvBytes;
        public long Mp4Bytes;
        public int ConvertedFileCount;
        public long RemainingWmvBytesToConvert;

    }

    public struct SuspiciouslySmallFilePair {

        public string WmvFile;
        public string Mp4File;
        public long WmvBytes;
        public long Mp4Bytes;

    }

}