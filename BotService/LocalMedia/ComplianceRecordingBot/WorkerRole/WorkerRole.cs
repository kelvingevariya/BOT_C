// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WorkerRole.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>
//   The worker role.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ComplianceRecordingBot.WorkerRole
{
    using CommonTools.Logging;
    using ComplianceRecordingBot.FrontEnd;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The worker role.
    /// </summary>
    public class WorkerRole : RoleEntryPoint
    {
        /// <summary>
        /// The cancellation token source.
        /// </summary>
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// The run complete event.
        /// </summary>
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        /// <summary>
        /// The graph logger.
        /// </summary>
        private readonly IGraphLogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerRole"/> class.
        /// </summary>
        public WorkerRole()
        {
            logger = new GraphLogger(typeof(WorkerRole).Assembly.GetName().Name, redirectToTrace: true);
            NLogHelper.SetupNLog();
        }

        /// <summary>
        /// The run.
        /// </summary>
        public override void Run()
        {
            logger.Info("[WorkerRole] is running");
            NLogHelper.Instance.Debug("[WorkerRole] is running");
            try
            {
                RunAsync(cancellationTokenSource.Token).Wait();
            }
            catch (Exception ex)
            {
                logger.Error($"[WorkerRole] Run error: {ex.Message}");
                NLogHelper.Instance.Debug($"[WorkerRole Error] Run Msg: {ex.Message}");
            }
            finally
            {
                runCompleteEvent.Set();
            }
        }

        /// <summary>
        /// The on start.
        /// </summary>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public override bool OnStart()
        {
            try
            {
                // Set the maximum number of concurrent connections
                if (RoleEnvironment.IsEmulated)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }
                else
                {
                    ServicePointManager.DefaultConnectionLimit = 12;
                }
                // Create and start the environment-independent service.
                Service.Instance.Initialize(new AzureConfiguration(logger), logger);
                Service.Instance.Start();
                var result = base.OnStart();
                logger.Info("[WorkerRole] has been started");
                NLogHelper.Instance.Debug("[WorkerRole] has been started");
                return result;
            }
            catch (Exception e)
            {
                logger.Error($"[WorkerRole] OnStart error: {e.Message}");
                NLogHelper.Instance.Debug($"[WorkerRole Error] OnStart Msg: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// The on stop.
        /// </summary>
        public override void OnStop()
        {
            Service.Instance.Stop();
            cancellationTokenSource.Cancel();
            runCompleteEvent.WaitOne();
            base.OnStop();
            logger.Info("[WorkerRole] has stopped");
            NLogHelper.Instance.Debug("[WorkerRole] has stopped");
        }

        /// <summary>
        /// The run async.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            //TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                logger.Info("Working");
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }
}