using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if  NETCOREAPP2_1
using Microsoft.Extensions.Logging.Internal;
#endif
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

#if NETCOREAPP3_0
        [Fact]
        public async Task CanWriteJsonFormat()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: null, formatter: new JsonLogFormatter());
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            logger.Log(_timestampOne, LogLevel.Information, 1, CreateFormattedValues("Info message {Value}", 123), null, (state, ex) => state.ToString());
            logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 2,
                CreateFormattedValues("Error message {Value}", "value"), null, (state, ex) => state.ToString());

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            var expected =
                @"{""Timestamp"":""2016-05-04T03:02:01+00:00"",""Level"":""Information"",""Category"":""Cat"",""Message"":""Info message 123"",""State"":{""Value"":123},""MessageTemplate"":""Info message {Value}""}"
                + Environment.NewLine
                + @"{""Timestamp"":""2016-05-04T04:02:01+00:00"",""Level"":""Error"",""Category"":""Cat"",""Message"":""Error message value"",""State"":{""Value"":""value""},""MessageTemplate"":""Error message {Value}""}"
                + Environment.NewLine;
            Assert.Equal(expected, File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504")));
        }

        [Fact]
        public async Task IncludesScopesInJsonFormat()
        {
            var provider = new TestFileLoggerProvider(TempPath, extension: null, formatter: new JsonLogFormatter(),
                includeScopes: true);
            ((ISupportExternalScope)provider).SetScopeProvider(new LoggerExternalScopeProvider());
            var logger = (BatchingLogger)provider.CreateLogger("Cat");

            await provider.IntervalControl.Pause;

            // annoying the hoops we have to jump through here.

            using(logger.BeginScope(new Dictionary<string, object>()
            {
                {"MyValues", "\"My escaped value \""},
                {"OtherValue", "test!"},
            }))
            using (logger.BeginScope("Test value"))
            using (logger.BeginScope("Test value"))
            {
                logger.Log(_timestampOne, LogLevel.Information, 1, CreateFormattedValues("Info message {Value}", 123), null, (state, ex) => state.ToString());
                logger.Log(_timestampOne.AddHours(1), LogLevel.Error, 2,
                    CreateFormattedValues("Error message {Value}", "value"), null, (state, ex) => state.ToString());
            }

            provider.IntervalControl.Resume();
            await provider.IntervalControl.Pause;

            var expected =
                @"{""Timestamp"":""2016-05-04T03:02:01+00:00"",""Level"":""Information"",""Category"":""Cat"",""Message"":""Info message 123"",""State"":{""Value"":123},""MessageTemplate"":""Info message {Value}"",""MyValues"":""\u0022My escaped value \u0022"",""OtherValue"":""test!"",""Scopes"":[""Test value"",""Test value""]}"
                + Environment.NewLine
                + @"{""Timestamp"":""2016-05-04T04:02:01+00:00"",""Level"":""Error"",""Category"":""Cat"",""Message"":""Error message value"",""State"":{""Value"":""value""},""MessageTemplate"":""Error message {Value}"",""MyValues"":""\u0022My escaped value \u0022"",""OtherValue"":""test!"",""Scopes"":[""Test value"",""Test value""]}"
                + Environment.NewLine;

            Assert.Equal(expected, File.ReadAllText(Path.Combine(TempPath, "LogFile.20160504")));
        }

        private static object CreateFormattedValues(string msg, params object[] values)
        {
            // annoying the hoops we have to jump through here.
            var formattedLogValuesType = typeof(ILoggerFactory).Assembly
                .GetType("Microsoft.Extensions.Logging.FormattedLogValues");

            var ctor = formattedLogValuesType.GetConstructor(new[] {typeof(string), typeof(object[])});
            return ctor.Invoke(new object[] {msg, values});
        }
#endif

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
