using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetEscapades.Extensions.Logging.RollingFile.Formatters;
using NetEscapades.Extensions.Logging.RollingFile.Internal;
using Xunit;

namespace NetEscapades.Extensions.Logging.RollingFile.Test
{
    public class FileLoggerTests : IDisposable
    {
        DateTimeOffset _timestampOne = new DateTimeOffset(2016, 05, 04, 03, 02, 01, TimeSpan.Zero);

        public FileLoggerTests()
        {
            TempPath = Path.GetTempFileName() + "_";
        }

        public string TempPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TempPath))
                {
                    Directory.Delete(TempPath, true);
                }
            }
            catch
            {
                // ignored
            }
        }

        [Fact]
        public async Task WritesToTextFile()
        {
            var provider = new TestFileLoggerProvider(TempPath);
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat: Info message" + Environment.NewLine +
                "2016-05-04 04:02:01.000 +00:00 [Error] Cat: Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504.txt")));
        }

        [Fact]
        public async Task RollsTextFile()
        {
            var provider = new TestFileLoggerProvider(TempPath);
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddDays(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat: Info message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504.txt")));

            Assert.Equal(
                "2016-05-05 03:02:01.000 +00:00 [Error] Cat: Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160505.txt")));
        }

        [Fact]
        public async Task RespectsMaxFileCount()
        {
            var provider = new TestFileLoggerProvider(TempPath, maxRetainedFiles: 5);
            var expectedFilenames = new[]
            {
                "LogFile.20160509.txt",
                "LogFile.20160510.txt",
                "LogFile.20160511.txt",
                "LogFile.20160512.txt",
                "LogFile.20160513.txt",
                "randomFile.txt"
            };
            await AssertExpectedFilenames(provider, expectedFilenames);
        }

        [Fact]
        public async Task RespectsMaxFileCountWithMultiFilePeriodicity()
        {
            var provider = new TestFileLoggerProvider(TempPath, maxRetainedFiles: 5, maxFilesPerPeriodicity: 5);
            var expectedFilenames = new[]
            {
                "LogFile.20160509.0.txt",
                "LogFile.20160510.0.txt",
                "LogFile.20160511.0.txt",
                "LogFile.20160512.0.txt",
                "LogFile.20160513.0.txt",
                "randomFile.txt"
            };
            await AssertExpectedFilenames(provider, expectedFilenames);
        }

        [Fact]
        public async Task RespectsMaxFileCountWithMultiFilePeriodicity2()
        {
            var provider = new TestFileLoggerProvider(TempPath, maxFileSize: 1, maxRetainedFiles: 5, maxFilesPerPeriodicity: 5);
            var expectedFilenames = new[]
            {
                "LogFile.20160509.0.txt",
                "LogFile.20160509.1.txt",
                "LogFile.20160510.0.txt",
                "LogFile.20160510.1.txt",
                "LogFile.20160511.0.txt",
                "LogFile.20160511.1.txt",
                "LogFile.20160512.0.txt",
                "LogFile.20160512.1.txt",
                "LogFile.20160513.0.txt",
                "LogFile.20160513.1.txt",
                "randomFile.txt"
            };
            await AssertExpectedFilenames(provider, expectedFilenames);
        }

        async Task AssertExpectedFilenames(TestFileLoggerProvider provider, string[] expectedFilenames)
        {
            Directory.CreateDirectory(TempPath);
            File.WriteAllText(Path.Combine(TempPath, "randomFile.txt"), "Text");

            var logger = (BatchingLogger) provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;
            var timestamp = _timestampOne;

            for (int i = 0; i < 10; i++)
            {
                logger.Log(timestamp, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
                logger.Log(timestamp.AddSeconds(1), LogLevel.Information, 0, "Info message", null, (state, ex) => state);
                logger.Log(timestamp.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

                timestamp = timestamp.AddDays(1);
            }

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            var actualFiles = new DirectoryInfo(TempPath)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(expectedFilenames, actualFiles);
        }

        [Fact]
        public async Task IncludesScopesWhenEnabled()
        {
            var provider = new TestFileLoggerProvider(TempPath, includeScopes: true);

            // this would normally be done by the ILoggerFactory
            ((ISupportExternalScope)provider).SetScopeProvider(new LoggerExternalScopeProvider());
            var logger = (BatchingLogger)provider.CreateLogger("Cat");


            await provider.IntervalControl.Pause;
            using (logger.BeginScope("Entering Scope <{ScopeName}>", "test"))
            {
                logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            }

            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat => Entering Scope <test>:"+ Environment.NewLine + 
                "Info message" + Environment.NewLine +
                "2016-05-04 04:02:01.000 +00:00 [Error] Cat:" + Environment.NewLine +
                "Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504.txt")));
        }

        [Fact]
        public async Task CorrectlySetsFileExtensionWithDot()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: ".log");
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat: Info message" + Environment.NewLine +
                "2016-05-04 04:02:01.000 +00:00 [Error] Cat: Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504.log")));
        }

        [Fact]
        public async Task CorrectlySetsFileExtensionWithoutDot()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: "log");
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat: Info message" + Environment.NewLine +
                "2016-05-04 04:02:01.000 +00:00 [Error] Cat: Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504.log")));
        }

        [Fact]
        public async Task CorrectlyHandlesNullFileExtension()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: null);
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "2016-05-04 03:02:01.000 +00:00 [Information] Cat: Info message" + Environment.NewLine +
                "2016-05-04 04:02:01.000 +00:00 [Error] Cat: Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504")));
        }

        [Fact]
        public void CanCreateProviderWithTheDefaultFormatter()
        {
            var option = new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions());

            var provider = new FileLoggerProvider(option, new List<ILogFormatter> {new SimpleLogFormatter()});

            Assert.NotNull(provider);
        }

        [Fact]
        public void WhenFormatterNotAvailable_Throws()
        {
            var option = new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions()
            {
                FormatterName = "unknown",
            });

            Assert.Throws<ArgumentException>(() =>
                new FileLoggerProvider(option, new List<ILogFormatter> {new SimpleLogFormatter()}));
        }

        [Fact]
        public void CanSelectFormatterByNameWhenMultiple()
        {
            var name = Guid.NewGuid().ToString().ToLowerInvariant();
            var option = new OptionsWrapperMonitor<FileLoggerOptions>(new FileLoggerOptions()
            {
                FormatterName = name,
            });

            var formatters = new List<ILogFormatter>
            {
                new SimpleLogFormatter(),
                new MessageOnlyFormatter(name),
            };

            var provider = new FileLoggerProvider(option, formatters);
        }

        [Fact]
        public async Task CanUseCustomFormat()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: null, formatter: new MessageOnlyFormatter());
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 0, "Info message", null, (state, ex) => state);
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 0, "Error message", null, (state, ex) => state);

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            Assert.Equal(
                "Info message" + Environment.NewLine +
                "Error message" + Environment.NewLine,
                File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504")));
        }

        public class MessageOnlyFormatter: ILogFormatter
        {
            public MessageOnlyFormatter(string name = "test")
            {
                Name = name;
            }

            public string Name { get; }
            public void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, StringBuilder stringBuilder)
            {
                stringBuilder.AppendLine(logEntry.Formatter(logEntry.State, logEntry.Exception));
            }
        }
    }
}
