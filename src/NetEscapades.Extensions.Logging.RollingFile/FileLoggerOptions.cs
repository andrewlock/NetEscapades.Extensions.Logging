using System;
using NetEscapades.Extensions.Logging.RollingFile.Internal;

namespace NetEscapades.Extensions.Logging.RollingFile
{
    /// <summary>
    /// Options for file logging.
    /// </summary>
    public class FileLoggerOptions : BatchingLoggerOptions
    {
        private int? _fileSizeLimit = 10 * 1024 * 1024;
        private int? _retainedFileCountLimit = 2;
        private string _fileName = "logs-";
        private string _extension = "txt";
        private PeriodicityOptions _periodicity = PeriodicityOptions.Daily;
        

        /// <summary>
        /// Gets or sets a strictly positive value representing the maximum log size in bytes or null for no limit.
        /// Once the log is full, no more messages will be appended.
        /// Defaults to <c>10MB</c>.
        /// </summary>
        public int? FileSizeLimit
        {
            get { return _fileSizeLimit; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(FileSizeLimit)} must be positive.");
                }
                _fileSizeLimit = value;
            }
        }

        /// <summary>
        /// Gets or sets a strictly positive value representing the maximum retained file count or null for no limit.
        /// Defaults to <c>2</c>.
        /// </summary>
        public int? RetainedFileCountLimit
        {
            get { return _retainedFileCountLimit; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(RetainedFileCountLimit)} must be positive.");
                }
                _retainedFileCountLimit = value;
            }
        }

        /// <summary>
        /// Gets or sets the filename prefix to use for log files.
        /// Defaults to <c>logs-</c>.
        /// </summary>
        public string FileName
        {
            get { return _fileName; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(nameof(value));
                }
                _fileName = value;
            }
        }

        /// <summary>
        /// Gets or sets the filename extension to use for log files.
        /// Defaults to <c>txt</c>.
        /// Will strip any prefixed .
        /// </summary>
        public string Extension
        {
            get { return _extension; }
            set { _extension = value?.TrimStart('.'); }
        }

        /// <summary>
        /// Gets or sets the periodicity for rolling over log files.
        /// </summary>
        public PeriodicityOptions Periodicity
        {
            get { return _periodicity; }
            set { _periodicity = value; }
        }

        /// <summary>
        /// The directory in which log files will be written, relative to the app process.
        /// Default to <c>Logs</c>
        /// </summary>
        /// <returns></returns>
        public string LogDirectory { get; set; } = "Logs";
    }
}
