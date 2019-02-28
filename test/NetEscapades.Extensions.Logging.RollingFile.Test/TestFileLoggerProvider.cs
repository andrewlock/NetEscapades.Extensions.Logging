using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetEscapades.Extensions.Logging.RollingFile.Test
{
    internal class TestFileLoggerProvider : FileLoggerProvider
    {
        internal ManualIntervalControl IntervalControl { get; } = new ManualIntervalControl();

        public TestFileLoggerProvider(
            string path,
            string fileName = "LogFile.",
            string extension = "txt",
            int maxFileSize = 32_000,
            int maxRetainedFiles = 100,
            bool includeScopes = false)
            : base(new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions()
            {
                LogDirectory = path,
                FileName = fileName,
                Extension = extension,
                FileSizeLimit = maxFileSize,
                RetainedFileCountLimit = maxRetainedFiles,
                IsEnabled = true,
                IncludeScopes = includeScopes
            }))
        {
        }

        protected override Task IntervalAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return IntervalControl.IntervalAsync();
        }
    }

}
