namespace CommonTools.Extensions
{
    using Microsoft.Graph.Communications.Common.Telemetry;
    using System.Diagnostics;

    /// <summary>
    /// IGraphLogger Extensions
    /// </summary>
    public static class LogExtensions
    {
        /// <summary>
        /// Level Error.
        /// </summary>
        /// <param name="logger">logger.</param>
        /// <param name="msg">msg.</param>
        /// <param name="level">level.</param>
        public static void Debug(this IGraphLogger logger, string msg, TraceLevel level = TraceLevel.Error)
        {
            if (logger != null)
            {
                logger.Log(level, $"[DebugLog] {msg}");
            }
        }
    }
}