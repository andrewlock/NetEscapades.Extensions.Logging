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
        private readonly int? _maxFileCount;
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
            _maxFileCount = loggerOptions.FilesPerPeriodicityLimit;
            _periodicity = loggerOptions.Periodicity;
        }

        /// <inheritdoc />
        protected override async Task WriteMessagesAsync(IEnumerable<LogMessage> messages, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_path);

            foreach (var group in messages.GroupBy(GetGrouping))
            {
                var baseName = GetBaseName(group.Key);
                var fullName = GetLogFilePath(baseName, group.Key);

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

        private string GetLogFilePath(string baseName, (int Year, int Month, int Day, int Hour, int Minute) fileNameGrouping)
        {
            if (IsMultiFileEnabled())
            {
                var fullPath = Path.Combine(_path, $"{baseName}.{_extension}");
                return IsAvailable(fullPath) ? fullPath : null;
            }

            var counter = GetCurrentCounter(baseName);

            while (counter < _maxFileCount)
            {
                var fullName = Path.Combine(_path,$"{baseName}.{counter}.{_extension}");
                if (!IsAvailable(fullName))
                {
                    counter++;
                    continue;
                }

                return fullName;
            }

            return null;

            bool IsAvailable(string filename)
            {
                var fileInfo = new FileInfo(filename);
                return !(_maxFileSize > 0 && fileInfo.Exists && fileInfo.Length > _maxFileSize);
            }

            bool IsMultiFileEnabled() => (_maxFileCount ?? 1) == 1;
        }

        private int GetCurrentCounter(string baseName)
        {
            try
            {
                var files = Directory.GetFiles(_path, $"{baseName}.*{_extension}");
                if (files.Length == 0)
                {
                    // No rolling file currently exists with the base name as pattern
                    return 0;
                }

                // Get file with highest counter
                var latestFile = files.OrderByDescending(file => file).First();

                var baseNameLength = Path.Combine(_path, baseName).Length + 1;
                var fileWithoutPrefix = latestFile
                    .AsSpan()
                    .Slice(baseNameLength);
                var indexOfPeriod = fileWithoutPrefix.IndexOf('.');
                if (indexOfPeriod < 0)
                {
                    // No additional dot could be found
                    return 0;
                }

                var counterSpan = fileWithoutPrefix.Slice(0, indexOfPeriod);
                if (int.TryParse(counterSpan.ToString(), out var counter))
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
