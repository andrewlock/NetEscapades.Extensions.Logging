#if NETCOREAPP3_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NetEscapades.Extensions.Logging.RollingFile.Formatters
{
    public class JsonLogFormatter: ILogFormatter
    {
        private const string MessageTemplateKey = "{OriginalFormat}";

        private static readonly JsonWriterOptions Options = new()
        {
            Indented = false,
        };

        public string Name => "json";

        public void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, StringBuilder stringBuilder)
        {
            var exception = logEntry.Exception;
            var message = logEntry.Formatter(logEntry.State, exception);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, Options);

            writer.WriteStartObject();
            writer.WriteString("Timestamp", logEntry.Timestamp);
            writer.WriteString("Level", GetLogLevelString(logEntry.LogLevel));
            writer.WriteString("Category", logEntry.Category);
            writer.WriteString("Message", message);
            if (exception != null)
            {
                var exceptionMessage = exception.ToString()
                    .Replace(Environment.NewLine, " ");
                writer.WriteString(nameof(Exception), exceptionMessage);
            }

            string messageTemplate = null;
            if (logEntry.State != null)
            {
                writer.WriteStartObject(nameof(logEntry.State));
                if (logEntry.State is IEnumerable<KeyValuePair<string, object>> stateProperties)
                {
                    foreach (KeyValuePair<string, object> item in stateProperties)
                    {
                        if (item.Key == MessageTemplateKey
                            && item.Value is string template)
                        {
                            messageTemplate = template;
                        }
                        else
                        {
                            WriteItem(writer, item);
                        }
                    }
                }
                else
                {
                    writer.WriteString("Message", logEntry.State.ToString());
                }
                writer.WriteEndObject();
            }

            if (!string.IsNullOrEmpty(messageTemplate))
            {
                writer.WriteString("MessageTemplate", messageTemplate);
            }

            if (scopeProvider != null)
            {
                var writerWrapper = new WriterWrapper(writer);
                scopeProvider.ForEachScope((scope, state) =>
                {
                    // Add dictionary scopes to the "root" object
                    if (scope is IEnumerable<KeyValuePair<string, object>> scopeItems)
                    {
                        foreach (KeyValuePair<string, object> item in scopeItems)
                        {
                            WriteItem(state.Writer, item);
                        }
                    }
                    else
                    {
                        state.Values.Add(scope); // add to list for inclusion in scope array
                    }
                }, writerWrapper);


                if (writerWrapper.Values.Any())
                {
                    writer.WriteStartArray("Scopes");
                    foreach (var value in writerWrapper.Values)
                    {
                        WriteValue(writer, value);
                    }
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            stringBuilder.AppendLine(Encoding.UTF8.GetString(stream.ToArray()));
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "Trace",
                LogLevel.Debug => "Debug",
                LogLevel.Information => "Information",
                LogLevel.Warning => "Warning",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Critical",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }

        private void WriteItem(Utf8JsonWriter writer, KeyValuePair<string, object> item)
        {
            var key = item.Key;
            switch (item.Value)
            {
                case bool boolValue:
                    writer.WriteBoolean(key, boolValue);
                    break;
                case byte byteValue:
                    writer.WriteNumber(key, byteValue);
                    break;
                case sbyte sbyteValue:
                    writer.WriteNumber(key, sbyteValue);
                    break;
                case char charValue:
#if NETCOREAPP
                    writer.WriteString(key, MemoryMarshal.CreateSpan(ref charValue, 1));
#else
                    writer.WriteString(key, charValue.ToString());
#endif
                    break;
                case decimal decimalValue:
                    writer.WriteNumber(key, decimalValue);
                    break;
                case double doubleValue:
                    writer.WriteNumber(key, doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumber(key, floatValue);
                    break;
                case int intValue:
                    writer.WriteNumber(key, intValue);
                    break;
                case uint uintValue:
                    writer.WriteNumber(key, uintValue);
                    break;
                case long longValue:
                    writer.WriteNumber(key, longValue);
                    break;
                case ulong ulongValue:
                    writer.WriteNumber(key, ulongValue);
                    break;
                case short shortValue:
                    writer.WriteNumber(key, shortValue);
                    break;
                case ushort ushortValue:
                    writer.WriteNumber(key, ushortValue);
                    break;
                case null:
                    writer.WriteNull(key);
                    break;
                default:
                    writer.WriteString(key, ToInvariantString(item.Value));
                    break;
            }
        }

        private void WriteValue(Utf8JsonWriter writer, object item)
        {
            switch (item)
            {
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;
                case byte byteValue:
                    writer.WriteNumberValue(byteValue);
                    break;
                case sbyte sbyteValue:
                    writer.WriteNumberValue(sbyteValue);
                    break;
                case char charValue:
#if NETCOREAPP
                    writer.WriteStringValue(MemoryMarshal.CreateSpan(ref charValue, 1));
#else
                    writer.WriteStringValue(charValue.ToString());
#endif
                    break;
                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumberValue(floatValue);
                    break;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;
                case uint uintValue:
                    writer.WriteNumberValue(uintValue);
                    break;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;
                case ulong ulongValue:
                    writer.WriteNumberValue(ulongValue);
                    break;
                case short shortValue:
                    writer.WriteNumberValue(shortValue);
                    break;
                case ushort ushortValue:
                    writer.WriteNumberValue(ushortValue);
                    break;
                case null:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteStringValue(ToInvariantString(item));
                    break;
            }
        }

        private static string ToInvariantString(object obj) => Convert.ToString(obj, CultureInfo.InvariantCulture);

        private readonly struct WriterWrapper
        {
            public readonly Utf8JsonWriter Writer;
            public readonly List<object> Values;

            public WriterWrapper(Utf8JsonWriter writer)
            {
                Writer = writer;
                Values = new List<object>();
            }
        }
    }
}
#endif