using System.Text;
using Microsoft.Extensions.Logging;

namespace NetEscapades.Extensions.Logging.RollingFile.Formatters
{
    /// <summary>
    /// Formats log messages that are written to the log file
    /// </summary>
    public interface ILogFormatter
    {
        /// <summary>
        /// Gets the name of the formatter
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Writes the log message to the specified StringBuilder.
        /// </summary>
        /// <param name="logEntry">The log entry.</param>
        /// <param name="scopeProvider">The provider of scope data.</param>
        /// <param name="stringBuilder">The string builder for building the message to write to the log file.</param>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, StringBuilder stringBuilder);
    }
}