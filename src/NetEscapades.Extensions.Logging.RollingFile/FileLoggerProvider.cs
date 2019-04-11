// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in https://github.com/aspnet/Logging for license information.
// https://github.com/aspnet/Logging/blob/2d2f31968229eddb57b6ba3d34696ef366a6c71b/src/Microsoft.Extensions.Logging.AzureAppServices/Internal/FileLoggerProvider.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetEscapades.Extensions.Logging.RollingFile.Internal;

namespace NetEscapades.Extensions.Logging.RollingFile
{
    /// <summary>
    /// An <see cref="ILoggerProvider" /> that writes logs to a file
    /// </summary>
    [ProviderAlias("File")]
    public class FileLoggerProvider : BatchingLoggerProvider
    {
        private readonly string _path;
        private readonly string _fileName;
        private readonly string _extension;
        private readonly int? _maxFileSize;
        private readonly int? _maxRetainedFiles;
        private readonly PeriodicityOptions _periodicity;

        /// <summary>
        /// Creates an instance of the <see cref="FileLoggerProvider" /> 
        /// </summary>
        /// <param name="options">The options object controlling the logger</param>
        public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> options) : base(options)
        {
            var loggerOptions = options.CurrentValue;
            _path = loggerOptions.LogDirectory;
            _fileName = loggerOptions.FileName;
            _extension = loggerOptions.Extension;
            _maxFileSize = loggerOptions.FileSizeLimit;
            _maxRetainedFiles = loggerOptions.RetainedFileCountLimit;
            _periodicity = loggerOptions.Periodicity;
        }

        /// <inheritdoc />
        protected override async Task WriteMessagesAsync(IEnumerable<LogMessage> messages, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_path);

            foreach (var group in messages.GroupBy(GetGrouping))
            {
                var fullName = GetLogFile(@group);

                if (fullName == null)
                {
                    return;
                }

                using (var streamWriter = File.AppendText(fullName))
                {
                    foreach (var item in group)
                    {
                        await streamWriter.WriteAsync(item.Message);
                    }
                }
            }

            RollFiles();
        }

        private string GetLogFile(IGrouping<(int Year, int Month, int Day, int Hour, int Minute), LogMessage> fileNameGrouping)
        {
            var counter = GetCurrentCounter(GetBaseName(fileNameGrouping.Key));
            
            while (counter < 2000)
            {
                var fullName = GetFullName(fileNameGrouping.Key, counter);
                var fileInfo = new FileInfo(fullName);
                if (_maxFileSize > 0 && fileInfo.Exists && fileInfo.Length > _maxFileSize)
                {
                    counter++;
                    continue;
                }

                return fullName;
            }

            return null;
        }

        private int GetCurrentCounter(string baseName)
        {
            try
            {
                var files = Directory.GetFiles(_path, $"{baseName}*.{_extension}");
                if (files.Length == 0)
                {
                    // No rolling file currently exists with the base name as pattern
                    return 0;
                }

                // Get file with highest counter
                var latestFile = files.OrderByDescending(file => file).First();

                var fileWithoutPrefix = latestFile.Substring(Path.Combine(_path, baseName).Length + 1);
                if (fileWithoutPrefix.IndexOf(".", StringComparison.Ordinal) < 0)
                {
                    // No additional dot could be found
                    return 0;
                }

                var counterString = fileWithoutPrefix.Substring(0, fileWithoutPrefix.IndexOf(".", StringComparison.Ordinal));
                if (int.TryParse(counterString, out var counter))
                {
                    return counter;
                }

                return 0;
            }
            catch (Exception)
            {
                return 0;
            }

        }

        private string GetBaseName((int Year, int Month, int Day, int Hour, int Minute) group)
        {
            switch (_periodicity)
            {
                case PeriodicityOptions.Minutely:
                    return $"{_fileName}{group.Year:0000}{group.Month:00}{group.Day:00}{group.Hour:00}{group.Minute:00}";
                case PeriodicityOptions.Hourly:
                    return $"{_fileName}{group.Year:0000}{group.Month:00}{group.Day:00}{group.Hour:00}";
                case PeriodicityOptions.Daily:
                    return $"{_fileName}{group.Year:0000}{group.Month:00}{group.Day:00}";
                case PeriodicityOptions.Monthly:
                    return $"{_fileName}{group.Year:0000}{group.Month:00}";
            }
            throw new InvalidDataException("Invalid periodicity");
        }

        private string GetFullName((int Year, int Month, int Day, int Hour, int Minute) group, int counter)
        {
            var baseName = GetBaseName(group);
            return Path.Combine(_path,$"{baseName}.{counter}.{_extension}");
        }

        private (int Year, int Month, int Day, int Hour, int Minute) GetGrouping(LogMessage message)
        {
            return (message.Timestamp.Year, message.Timestamp.Month, message.Timestamp.Day, message.Timestamp.Hour, message.Timestamp.Minute);
        }

        /// <summary>
        /// Deletes old log files, keeping a number of files defined by <see cref="FileLoggerOptions.RetainedFileCountLimit" />
        /// </summary>
        protected void RollFiles()
        {
            if (_maxRetainedFiles > 0)
            {
                var files = new DirectoryInfo(_path)
                    .GetFiles(_fileName + "*")
                    .OrderByDescending(f => f.Name)
                    .Skip(_maxRetainedFiles.Value);

                foreach (var item in files)
                {
                    item.Delete();
                }
            }
        }
    }
}
