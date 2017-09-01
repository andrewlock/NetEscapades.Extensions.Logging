using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetEscapades.AspNetCore.Logging.RollingFile.Test
{
    internal class TestFileLoggerProvider : FileLoggerProvider
    {
        internal ManualIntervalControl IntervalControl { get; } = new ManualIntervalControl();

        public TestFileLoggerProvider(
            string path,
            string fileName = "LogFile.",
            int maxFileSize = 32_000,
            int maxRetainedFiles = 100)
            : base(new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions()
            {
                LogDirectory = path,
                FileName = fileName,
                FileSizeLimit = maxFileSize,
                RetainedFileCountLimit = maxRetainedFiles,
                IsEnabled = true
            }))
        {
        }

        protected override Task IntervalAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return IntervalControl.IntervalAsync();
        }
    }

}
