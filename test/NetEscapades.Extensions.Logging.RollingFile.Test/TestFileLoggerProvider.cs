using System;
using System.Threading;
using System.Threading.Tasks;
using NetEscapades.Extensions.Logging.RollingFile.Formatters;

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
            int maxFilesPerPeriodicity = 1,
            bool includeScopes = false,
            ILogFormatter formatter = null)
            : base(new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions()
            {
                LogDirectory = path,
                FileName = fileName,
                Extension = extension,
                FileSizeLimit = maxFileSize,
                RetainedFileCountLimit = maxRetainedFiles,
                FilesPerPeriodicityLimit = maxFilesPerPeriodicity,
                IsEnabled = true,
                IncludeScopes = includeScopes,
                FormatterName = formatter?.Name ?? "simple"
            }), new[] { formatter ?? new SimpleLogFormatter()})
        {
        }

        protected override Task IntervalAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return IntervalControl.IntervalAsync();
        }
    }
}
