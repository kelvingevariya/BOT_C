// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AzureConfiguration.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>
//   The configuration for azure.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ComplianceRecordingBot.WorkerRole
{
    using CommonTools.Logging;
    using ComplianceRecordingBot.FrontEnd.Contract;
    using ComplianceRecordingBot.FrontEnd.Http;
    using Microsoft.Azure;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Skype.Bots.Media;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// Reads the Configuration from service Configuration.
    /// </summary>
    internal class AzureConfiguration : IConfiguration
    {
        /// <summary>
        /// DomainNameLabel in NetworkConfiguration in .cscfg  <PublicIP name="instancePublicIP" domainNameLabel="pip"/>
        /// If the below changes, please change in the cscfg as well.
        /// </summary>
        public const string DomainNameLabel = "pip";

        /// <summary>
        /// The default endpoint key.
        /// </summary>
        private const string DefaultEndpointKey = "DefaultEndpoint";

        /// <summary>
        /// The instance call control endpoint key.
        /// </summary>
        private const string InstanceCallControlEndpointKey = "InstanceCallControlEndpoint";

        /// <summary>
        /// The instance media control endpoint key.
        /// </summary>
        private const string InstanceMediaControlEndpointKey = "InstanceMediaControlEndpoint";

        /// <summary>
        /// The service dns name key.
        /// </summary>
        private const string ServiceDnsNameKey = "ServiceDNSName";

        /// <summary>
        /// The service cname key.
        /// </summary>
        private const string ServiceCNameKey = "ServiceCNAME";

        /// <summary>
        /// The place call endpoint URL key.
        /// </summary>
        private const string PlaceCallEndpointUrlKey = "PlaceCallEndpointUrl";

        /// <summary>
        /// The default certificate key.
        /// </summary>
        private const string DefaultCertificateKey = "DefaultCertificate";

        /// <summary>
        /// The Bot Name key.
        /// </summary>
        private const string BotNameKey = "BotName";

        /// <summary>
        /// The Microsoft app id key.
        /// </summary>
        private const string AadAppIdKey = "AadAppId";

        /// <summary>
        /// The Microsoft app password key.
        /// </summary>
        private const string AadAppSecretKey = "AadAppSecret";

        /// <summary>
        /// The default Microsoft app id value.
        /// </summary>
        private const string DefaultAadAppIdValue = "$AadAppId$";

        /// <summary>
        /// The default Microsoft app password value.
        /// </summary>
        private const string DefaultAadAppSecretValue = "$AadAppSecret$";

        private const string AuthWrapperTenantIdKey = "AuthWrapperTenantId";

        //Recording Merge Tools
        private const string DefaultOutputFolderKey = "DefaultOutputFolder";

        private const string AudioFolderKey = "AudioFolder";
        private const string VideoFolderKey = "VideoFolder";
        private const string VBSSFolderKey = "VBSSFolder";
        private const string WaitForCheckFileSecondsKey = "WaitForCheckFileSeconds";

        //Graph API Tools
        private const string TenantIdKey = "TenantId";

        private const string OrganizerObjectIdKey = "OrganizerObjectId";
        private const string ChannelIdKey = "ChannelId";
        private const string GroupNameKey = "GroupName";
        private const string HostNameKey = "HostName";
        private const string RelativePathKey = "RelativePath";

        //Power Shell Tools
        private const string TeamsAdminAccountKey = "TeamsAdminAccount";

        private const string TeamsAdminPwdKey = "TeamsAdminPwd";
        private const string ComplianceRecordingPolicyNameKey = "ComplianceRecordingPolicyName";

        // Azure Storage Tools
        private const string BlobServiceEndpointKey = "BlobServiceEndpoint";

        private const string StorageAccountNameKey = "StorageAccountName";
        private const string StorageAccountKeyKey = "StorageAccountKey";

        /// <summary>
        /// localPort specified in <InputEndpoint name="DefaultCallControlEndpoint" protocol="tcp" port="443" localPort="9441" />
        /// in .csdef. This is needed for running in emulator. Currently only messaging can be debugged in the emulator.
        /// Media debugging in emulator will be supported in future releases.
        /// </summary>
        private const int DefaultPort = 9441;

        /// <summary>
        /// Graph logger.
        /// </summary>
        private IGraphLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureConfiguration"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public AzureConfiguration(IGraphLogger logger)
        {
            _logger = logger;
            Initialize();
        }

        /// <inheritdoc/>
        public string ServiceDnsName { get; private set; }

        /// <summary>
        /// Gets the service cname.
        /// </summary>
        public string ServiceCname { get; private set; }

        /// <inheritdoc/>
        public IEnumerable<Uri> CallControlListeningUrls { get; private set; }

        /// <inheritdoc/>
        public Uri CallControlBaseUrl { get; private set; }

        /// <inheritdoc/>
        public Uri PlaceCallEndpointUrl { get; private set; }

        /// <inheritdoc/>
        public MediaPlatformSettings MediaPlatformSettings { get; private set; }

        /// <inheritdoc/>
        public string BotName { get; private set; }

        /// <inheritdoc/>
        public string AadAppId { get; private set; }

        /// <inheritdoc/>
        public string AadAppSecret { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string AuthWrapperTenantId { get; private set; }

        //Recording Merge Tools
        /// <summary>
        ///
        /// </summary>
        public string DefaultOutputFolder { get; private set; }

        /// <summary>
        /// Audio
        /// </summary>
        public string AudioFolder { get; private set; }

        /// <summary>
        /// Video
        /// </summary>
        public string VideoFolder { get; private set; }

        /// <summary>
        /// VBSS
        /// </summary>
        public string VBSSFolder { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public int WaitForCheckFileSeconds { get; private set; }

        //Graph API Tools
        /// <summary>
        ///
        /// </summary>
        public string TenantId { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string OrganizerObjectId { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string ChannelId { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string GroupName { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string HostName { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string RelativePath { get; private set; }

        //Power Shell Tools
        /// <summary>
        ///
        /// </summary>
        public string TeamsAdminAccount { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string TeamsAdminPwd { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string ComplianceRecordingPolicyName { get; private set; }

        // Azure Storage Tools
        /// <summary>
        ///
        /// </summary>
        public string BlobServiceEndpoint { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string StorageAccountName { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string StorageAccountKey { get; private set; }

        /// <summary>
        /// Initialize from serviceConfig.
        /// </summary>
        public void Initialize()
        {
            _logger.Info("AzureConfiguration Initialize");
            NLogHelper.Instance.Debug("[AzureConfiguration] Initialize");
            // Collect config values from Azure config.
            TraceEndpointInfo();
            ServiceDnsName = GetString(ServiceDnsNameKey);
            NLogHelper.Instance.Debug($"[AzureConfiguration] DnsName: {ServiceDnsName}");
            ServiceCname = GetString(ServiceCNameKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] Cname: {ServiceCname}");
            if (string.IsNullOrEmpty(ServiceCname))
            {
                ServiceCname = ServiceDnsName;
            }
            var placeCallEndpointUrlStr = GetString(PlaceCallEndpointUrlKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] PlaceCallEndpointUrlStr: {placeCallEndpointUrlStr}");
            if (!string.IsNullOrEmpty(placeCallEndpointUrlStr))
            {
                PlaceCallEndpointUrl = new Uri(placeCallEndpointUrlStr);
            }
            X509Certificate2 defaultCertificate = GetCertificateFromStore(DefaultCertificateKey);
            RoleInstanceEndpoint instanceCallControlEndpoint = RoleEnvironment.IsEmulated ? null : GetEndpoint(InstanceCallControlEndpointKey);
            RoleInstanceEndpoint defaultEndpoint = GetEndpoint(DefaultEndpointKey);
            RoleInstanceEndpoint mediaControlEndpoint = RoleEnvironment.IsEmulated ? null : GetEndpoint(InstanceMediaControlEndpointKey);
            int instanceCallControlInternalPort = RoleEnvironment.IsEmulated ? DefaultPort : instanceCallControlEndpoint.IPEndpoint.Port;
            string instanceCallControlInternalIpAddress = RoleEnvironment.IsEmulated
                ? IPAddress.Loopback.ToString()
                : instanceCallControlEndpoint.IPEndpoint.Address.ToString();
            int instanceCallControlPublicPort = RoleEnvironment.IsEmulated ? DefaultPort : instanceCallControlEndpoint.PublicIPEndpoint.Port;
            int mediaInstanceInternalPort = RoleEnvironment.IsEmulated ? 8445 : mediaControlEndpoint.IPEndpoint.Port;
            int mediaInstancePublicPort = RoleEnvironment.IsEmulated ? 13016 : mediaControlEndpoint.PublicIPEndpoint.Port;
            string instanceCallControlIpEndpoint = string.Format("{0}:{1}", instanceCallControlInternalIpAddress, instanceCallControlInternalPort);
            AadAppId = ConfigurationManager.AppSettings[AadAppIdKey];
            NLogHelper.Instance.Debug($"[AzureConfiguration] AadAppId: {AadAppId}");
            if (string.IsNullOrEmpty(AadAppId) || string.Equals(AadAppId, DefaultAadAppIdValue))
            {
                throw new ConfigurationException("AadAppId", "Update app.config in WorkerRole with AppId from the bot registration portal");
            }
            AadAppSecret = ConfigurationManager.AppSettings[AadAppSecretKey];
            NLogHelper.Instance.Debug($"[AzureConfiguration] AadAppSecret: {AadAppSecret}");
            if (string.IsNullOrEmpty(AadAppSecret) || string.Equals(AadAppSecret, DefaultAadAppSecretValue))
            {
                throw new ConfigurationException("AadAppSecret", "Update app.config in WorkerRole with BotSecret from the bot registration portal");
            }
            AuthWrapperTenantId = GetString(AuthWrapperTenantIdKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] AuthWrapperTenantId:{AuthWrapperTenantId}");
            BotName = ConfigurationManager.AppSettings[BotNameKey];
            NLogHelper.Instance.Debug($"[AzureConfiguration] BotName: {BotName}");
            List<Uri> controlListenUris = new List<Uri>();
            if (RoleEnvironment.IsEmulated)
            {
                // Create structured config objects for service.
                CallControlBaseUrl = new Uri(string.Format(
                    "https://{0}/{1}/{2}",
                    ServiceCname,
                    HttpRouteConstants.CallSignalingRoutePrefix,
                    HttpRouteConstants.OnNotificationRequestRoute));
                controlListenUris.Add(new Uri("https://" + defaultEndpoint.IPEndpoint.Address + ":" + DefaultPort + "/"));
                controlListenUris.Add(new Uri("http://" + defaultEndpoint.IPEndpoint.Address + ":" + (DefaultPort + 1) + "/"));
            }
            else
            {
                // Create structured config objects for service.
                CallControlBaseUrl = new Uri(string.Format(
                    "https://{0}:{1}/{2}/{3}",
                    ServiceCname,
                    instanceCallControlPublicPort,
                    HttpRouteConstants.CallSignalingRoutePrefix,
                    HttpRouteConstants.OnNotificationRequestRoute));
                controlListenUris.Add(new Uri("https://" + instanceCallControlIpEndpoint + "/"));
                controlListenUris.Add(new Uri("https://" + defaultEndpoint.IPEndpoint + "/"));
            }
            TraceConfigValue("CallControlCallbackUri", CallControlBaseUrl);
            CallControlListeningUrls = controlListenUris;
            foreach (Uri uri in CallControlListeningUrls)
            {
                TraceConfigValue("Call control listening Uri", uri);
            }
            IPAddress publicInstanceIpAddress = RoleEnvironment.IsEmulated
                ? IPAddress.Any
                : GetInstancePublicIpAddress(ServiceDnsName);
            //Media url for bot(eg: 1.bot.contoso.com)
            string serviceFqdn = RoleEnvironment.IsEmulated ? "0.ngrok.skype-graph-test.net" : ServiceCname;
            MediaPlatformSettings = new MediaPlatformSettings()
            {
                MediaPlatformInstanceSettings = new MediaPlatformInstanceSettings()
                {
                    CertificateThumbprint = defaultCertificate.Thumbprint,
                    InstancePublicIPAddress = publicInstanceIpAddress, //new IPAddress(0x0)
                    ServiceFqdn = serviceFqdn,
                    InstanceInternalPort = mediaInstanceInternalPort, // Azure、Localhost media port
                    InstancePublicPort = mediaInstancePublicPort, // Azure、Ngrok exposed remote media port
                },
                ApplicationId = AadAppId,
            };
            //Recording Merge Tools
            DefaultOutputFolder = GetString(DefaultOutputFolderKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] DefaultOutputFolder:{DefaultOutputFolder}");
            AudioFolder = GetString(AudioFolderKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] AudioFolder:{AudioFolder}");
            VideoFolder = GetString(VideoFolderKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] VideoFolder:{VideoFolder}");
            VBSSFolder = GetString(VBSSFolderKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] VBSSFolder:{VBSSFolder}");
            var waitForCheckFileSeconds = GetString(WaitForCheckFileSecondsKey, true);
            WaitForCheckFileSeconds = string.IsNullOrWhiteSpace(waitForCheckFileSeconds) ? 120 : int.Parse(waitForCheckFileSeconds);
            NLogHelper.Instance.Debug($"[AzureConfiguration] WaitForCheckFileSeconds:{WaitForCheckFileSeconds}");
            //Graph API Tools
            TenantId = GetString(TenantIdKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] TenantId:{TenantId}");
            OrganizerObjectId = GetString(OrganizerObjectIdKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] OrganizerObjectId:{OrganizerObjectId}");
            ChannelId = GetString(ChannelIdKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] ChannelId:{ChannelId}");
            GroupName = GetString(GroupNameKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] GroupName:{GroupName}");
            HostName = GetString(HostNameKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] HostName:{HostName}");
            RelativePath = GetString(RelativePathKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] RelativePath:{RelativePath}");
            //Power Shell Tools
            TeamsAdminAccount = GetString(TeamsAdminAccountKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] TeamsAdminAccount:{TeamsAdminAccount}");
            TeamsAdminPwd = GetString(TeamsAdminPwdKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] TeamsAdminPwd:{TeamsAdminPwd}");
            ComplianceRecordingPolicyName = GetString(ComplianceRecordingPolicyNameKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] ComplianceRecordingPolicyName:{ComplianceRecordingPolicyName}");
            // Azure Storage Tools
            BlobServiceEndpoint = GetString(BlobServiceEndpointKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] BlobServiceEndpoint:{BlobServiceEndpoint}");
            StorageAccountName = GetString(StorageAccountNameKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] StorageAccountName:{StorageAccountName}");
            StorageAccountKey = GetString(StorageAccountKeyKey, true);
            NLogHelper.Instance.Debug($"[AzureConfiguration] StorageAccountKey:{StorageAccountKey}");
            NLogHelper.Instance.Debug($"[AzureConfiguration] MediaPlatformSettings CertificateThumbprint: {defaultCertificate.Thumbprint} InstancePublicIPAddress:{publicInstanceIpAddress} ServiceFqdn:{serviceFqdn} InstanceInternalPort:{mediaInstanceInternalPort} InstancePublicPort:{mediaInstancePublicPort}");
        }

        /// <summary>
        /// Dispose the Configuration.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Write endpoint info into the debug logs.
        /// </summary>
        private void TraceEndpointInfo()
        {
            string[] endpoints = RoleEnvironment.IsEmulated
                ? new string[] { DefaultEndpointKey }
                : new string[] { DefaultEndpointKey, InstanceMediaControlEndpointKey };
            foreach (string endpointName in endpoints)
            {
                RoleInstanceEndpoint endpoint = GetEndpoint(endpointName);
                StringBuilder info = new StringBuilder();
                info.AppendFormat("Internal=https://{0}, ", endpoint.IPEndpoint);
                string publicInfo = endpoint.PublicIPEndpoint == null ? "-" : endpoint.PublicIPEndpoint.Port.ToString();
                info.AppendFormat("PublicPort={0}", publicInfo);
                TraceConfigValue(endpointName, info);
            }
        }

        /// <summary>
        /// Write debug entries for the configuration.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <param name="value">Configuration value.</param>
        private void TraceConfigValue(string key, object value)
        {
            _logger.Info($"{key} ->{value}");
        }

        /// <summary>
        /// Lookup endpoint by its name.
        /// </summary>
        /// <param name="name">Endpoint name.</param>
        /// <returns>Role instance endpoint.</returns>
        private RoleInstanceEndpoint GetEndpoint(string name)
        {
            if (!RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.TryGetValue(name, out RoleInstanceEndpoint endpoint))
            {
                throw new ConfigurationException(name, $"No endpoint with name '{name}' was found.");
            }
            return endpoint;
        }

        /// <summary>
        /// Lookup configuration value.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <param name="allowEmpty">If empty configurations are allowed.</param>
        /// <returns>Configuration value, if found.</returns>
        private string GetString(string key, bool allowEmpty = false)
        {
            string s = CloudConfigurationManager.GetSetting(key);
            TraceConfigValue(key, s);
            if (!allowEmpty && string.IsNullOrWhiteSpace(s))
            {
                throw new ConfigurationException(key, "The Configuration value is null or empty.");
            }
            return s;
        }

        /// <summary>
        /// Helper to search the certificate store by its thumbprint.
        /// </summary>
        /// <param name="key">Configuration key containing the Thumbprint to search.</param>
        /// <returns>Certificate if found.</returns>
        private X509Certificate2 GetCertificateFromStore(string key)
        {
            string thumbprint = GetString(key);
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            try
            {
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                if (certs.Count != 1)
                {
                    throw new ConfigurationException(key, $"No certificate with thumbprint {thumbprint} was found in the machine store.");
                }
                return certs[0];
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// Get the PIP for this instance.
        /// </summary>
        /// <param name="publicFqdn">DNS name for this service.</param>
        /// <returns>IPAddress.</returns>
        private IPAddress GetInstancePublicIpAddress(string publicFqdn)
        {
            // get the instanceId for the current instance. It will be of the form  XXMediaBotRole_IN_0. Look for IN_ and then extract the number after it
            // Assumption: in_<instanceNumber> will the be the last in the instanceId
            //string instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            //int instanceIdIndex = instanceId.IndexOf(InstanceIdToken, StringComparison.OrdinalIgnoreCase);
            //if (!int.TryParse(instanceId.Substring(instanceIdIndex + InstanceIdToken.Length), out int instanceNumber))
            //{
            //    var err = $"Couldn't extract Instance index from {instanceId}";
            //    graphLogger.Error(err);
            //    throw new Exception(err);
            //}
            // for example: instance0 for fooservice.cloudapp.net will have hostname as pip.0.fooservice.cloudapp.net
            //string instanceHostName = DomainNameLabel + "." + instanceNumber + "." + publicFqdn;
            //IPAddress[] instanceAddresses = Dns.GetHostEntry(instanceHostName).AddressList;
            //if (instanceAddresses.Length == 0)
            //{
            //    throw new InvalidOperationException("Could not resolve the PIP hostname. Please make sure that PIP is properly configured for the service");
            //}
            //return instanceAddresses[0];
            return IPAddress.Parse("0.0.0.0");
        }
    }
}