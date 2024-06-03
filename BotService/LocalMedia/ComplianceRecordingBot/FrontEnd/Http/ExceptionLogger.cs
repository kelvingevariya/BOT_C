namespace ComplianceRecordingBot.FrontEnd.Http
{
    using Microsoft.Graph.Communications.Common.Telemetry;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http.ExceptionHandling;

    /// <summary>
    /// The exception logger.
    /// </summary>
    public class ExceptionLogger : IExceptionLogger
    {
        private IGraphLogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionLogger"/> class.
        /// </summary>
        /// <param name="logger">Graph logger.</param>
        public ExceptionLogger(IGraphLogger logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public Task LogAsync(ExceptionLoggerContext context, CancellationToken cancellationToken)
        {
            this.logger.Error(context.Exception, "Exception processing HTTP request.");
            return Task.CompletedTask;
        }
    }
}