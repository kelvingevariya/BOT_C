// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Service.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>
//   Service is the main entry point independent of Azure.  Anyone instantiating Service needs to first
//   initialize the DependencyResolver.  Calling Start() on the Service starts the HTTP server that will
//   listen for incoming Conversation requests from the Skype Platform.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ComplianceRecordingBot.FrontEnd
{
    using CommonTools.Logging;
    using ComplianceRecordingBot.FrontEnd.Contract;
    using ComplianceRecordingBot.FrontEnd.Http;
    using ComplianceRecordingBot.FrontEnd.ServiceSetup;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Owin.Hosting;
    using System;

    /// <summary>
    /// Service is the main entry point independent of Azure.  Anyone instantiating Service needs to first
    /// initialize the DependencyResolver.  Calling Start() on the Service starts the HTTP server that will
    /// listen for incoming Conversation requests from the Skype Platform.
    /// </summary>
    public class Service
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly Service Instance = new Service();

        /// <summary>
        /// The sync lock.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// The call http server.
        /// </summary>
        private IDisposable callHttpServer;

        /// <summary>
        /// Is the service started.
        /// </summary>
        private bool started;

        /// <summary>
        /// Graph logger instance.
        /// </summary>
        private IGraphLogger _logger;

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// Instantiate a custom server (e.g. for testing).
        /// </summary>
        /// <param name="config">The configuration to initialize.</param>
        /// <param name="logger">Logger instance.</param>
        public void Initialize(IConfiguration config, IGraphLogger logger)
        {
            Configuration = config;
            _logger = logger;
        }

        /// <summary>
        /// Start the service.
        /// </summary>
        public void Start()
        {
            _logger.Info("[Service] Start");
            NLogHelper.Instance.Debug("[Service] Start");

            lock (syncLock)
            {
                if (started)
                {
                    NLogHelper.Instance.Debug("[Service] The service is already started.");
                    throw new InvalidOperationException("[Service] The service is already started.");
                }

                var settings = new AzureSettings();
                settings.Initialize(Configuration);

                Bot.Bot.Instance.Initialize(this, _logger, settings);

                // Start HTTP server for calls
                var callStartOptions = new StartOptions();
                foreach (var url in Configuration.CallControlListeningUrls)
                {
                    callStartOptions.Urls.Add(url.ToString());
                }

                _logger.Info("[Service] WebApp Start");
                NLogHelper.Instance.Debug("[Service] WebApp Start");
                callHttpServer = WebApp.Start(
                    callStartOptions,
                    (appBuilder) =>
                    {
                        var startup = new HttpConfigurationInitializer();
                        startup.ConfigureSettings(appBuilder, _logger);
                    });

                started = true;
            }
        }

        /// <summary>
        /// Stop the service.
        /// </summary>
        public void Stop()
        {
            _logger.Info("[Service] Stop");
            NLogHelper.Instance.Debug("[Service] Stop");

            lock (syncLock)
            {
                if (!started)
                {
                    NLogHelper.Instance.Debug("[Service] The service is already stopped.");
                    throw new InvalidOperationException("[Service] The service is already stopped.");
                }

                started = false;

                callHttpServer.Dispose();
                Bot.Bot.Instance.Dispose();
            }
        }
    }
}