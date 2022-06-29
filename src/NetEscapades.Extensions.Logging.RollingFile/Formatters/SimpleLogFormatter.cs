using System.Text;
using Microsoft.Extensions.Logging;

namespace NetEscapades.Extensions.Logging.RollingFile.Formatters
{
    /// <summary>
    /// A simple formatter for log messages
    /// </summary>
    public class SimpleLogFormatter : ILogFormatter
    {
        public string Name => "simple";

        /// <inheritdoc />
        public void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, StringBuilder builder)
        {
            builder.Append(logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append(" [");
            builder.Append(logEntry.LogLevel.ToString());
            builder.Append("] ");
            builder.Append(logEntry.Category);

            if (scopeProvider != null)
            {
                scopeProvider.ForEachScope((scope, stringBuilder) =>
                {
                    stringBuilder.Append(" => ").Append(scope);
                }, builder);

                builder.Append(':').AppendLine();
            }
            else
            {
                builder.Append(": ");
            }

            builder.AppendLine(logEntry.Formatter(logEntry.State, logEntry.Exception));

            if (logEntry.Exception != null)
            {
                builder.AppendLine(logEntry.Exception.ToString());
            }
        }
    }
}