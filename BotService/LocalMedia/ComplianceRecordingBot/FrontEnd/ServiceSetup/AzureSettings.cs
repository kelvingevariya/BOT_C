using ComplianceRecordingBot.FrontEnd.Contract;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;

namespace ComplianceRecordingBot.FrontEnd.ServiceSetup
{
    /// <summary>
    /// Class AzureSettings.
    /// </summary>
    public class AzureSettings
    {
        /// <summary>
        /// Gets or sets the name of the bot.
        /// </summary>
        /// <value>The name of the bot.</value>
        public string BotName { get; set; }

        /// <summary>
        /// Gets or sets the name of the service DNS.
        /// </summary>
        /// <value>The name of the service DNS.</value>
        public string ServiceDnsName { get; set; }

        /// <summary>
        /// Gets or sets the service cname.
        /// </summary>
        /// <value>The service cname.</value>
        public string ServiceCname { get; set; }

        /// <summary>
        /// Gets or sets the call control listening urls.
        /// </summary>
        /// <value>The call control listening urls.</value>
        public IEnumerable<Uri> CallControlListeningUrls { get; set; }

        /// <summary>
        /// Gets or sets the call control base URL.
        /// </summary>
        /// <value>The call control base URL.</value>
        public Uri CallControlBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the place call endpoint URL.
        /// </summary>
        /// <value>The place call endpoint URL.</value>
        public Uri PlaceCallEndpointUrl { get; set; }

        /// <summary>
        /// Gets the media platform settings.
        /// </summary>
        /// <value>The media platform settings.</value>
        public MediaPlatformSettings MediaPlatformSettings { get; private set; }

        /// <summary>
        /// Gets or sets the aad application identifier.
        /// </summary>
        /// <value>The aad application identifier.</value>
        public string AadAppId { get; set; }

        /// <summary>
        /// Gets or sets the aad application secret.
        /// </summary>
        /// <value>The aad application secret.</value>
        public string AadAppSecret { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string AuthWrapperTenantId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string DefaultOutputFolder { get; set; }

        /// <summary>
        /// Audio
        /// </summary>
        public string AudioFolder { get; set; }

        /// <summary>
        /// Video
        /// </summary>
        public string VideoFolder { get; set; }

        /// <summary>
        /// VBSS
        /// </summary>
        public string VBSSFolder { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int WaitForCheckFileSeconds { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string OrganizerObjectId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [capture events].
        /// </summary>
        /// <value><c>true</c> if [capture events]; otherwise, <c>false</c>.</value>
        public bool CaptureEvents { get; set; } = false;

        /// <summary>
        /// Gets or sets the events folder.
        /// </summary>
        /// <value>The events folder.</value>
        public string EventsFolder { get; set; } = "EventsFolder";

        // Event Grid Settings
        /// <summary>
        /// Gets or sets the name of the topic.
        /// </summary>
        /// <value>The name of the topic.</value>
        public string TopicName { get; set; } = "recordingbotevents";

        /// <summary>
        /// Gets or sets the name of the region.
        /// </summary>
        /// <value>The name of the region.</value>
        public string RegionName { get; set; } = "southeastasia";

        /// <summary>
        /// Gets or sets the topic key.
        /// </summary>
        /// <value>The topic key.</value>
        public string TopicKey { get; set; } = "";

        /// <summary>
        /// Gets or sets the audio settings.
        /// </summary>
        /// <value>The audio settings.</value>
        public AudioSettings AudioSettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is stereo.
        /// </summary>
        /// <value><c>true</c> if this instance is stereo; otherwise, <c>false</c>.</value>
        public bool IsStereo { get; set; } = false;

        /// <summary>
        /// Gets or sets the wav sample rate.
        /// </summary>
        /// <value>The wav sample rate.</value>
        public int WAVSampleRate { get; set; } = 0;

        /// <summary>
        /// Gets or sets the wav quality.
        /// </summary>
        /// <value>The wav quality.</value>
        public int WAVQuality { get; set; } = 100;

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
        /// Initializes this instance.
        /// </summary>
        /// <param name="azureConfiguration"></param>
        public void Initialize(IConfiguration azureConfiguration)
        {
            this.AadAppId = azureConfiguration.AadAppId;
            this.AadAppSecret = azureConfiguration.AadAppSecret;
            this.AuthWrapperTenantId = azureConfiguration.AuthWrapperTenantId;
            this.BotName = azureConfiguration.BotName;
            this.ServiceDnsName = azureConfiguration.ServiceDnsName;
            this.ServiceCname = azureConfiguration.ServiceCname;
            this.CallControlBaseUrl = azureConfiguration.CallControlBaseUrl;
            this.CallControlListeningUrls = azureConfiguration.CallControlListeningUrls;
            this.PlaceCallEndpointUrl = azureConfiguration.PlaceCallEndpointUrl;
            this.DefaultOutputFolder = azureConfiguration.DefaultOutputFolder;
            this.AudioFolder = azureConfiguration.AudioFolder;
            this.VideoFolder = azureConfiguration.VideoFolder;
            this.VBSSFolder = azureConfiguration.VBSSFolder;
            this.WaitForCheckFileSeconds = azureConfiguration.WaitForCheckFileSeconds;
            this.TenantId = azureConfiguration.TenantId;
            this.OrganizerObjectId = azureConfiguration.OrganizerObjectId;
            this.ChannelId = azureConfiguration.ChannelId;
            this.GroupName = azureConfiguration.GroupName;
            this.HostName = azureConfiguration.HostName;
            this.RelativePath = azureConfiguration.RelativePath;
            this.TeamsAdminAccount = azureConfiguration.TeamsAdminAccount;
            this.TeamsAdminPwd = azureConfiguration.TeamsAdminPwd;
            this.ComplianceRecordingPolicyName = azureConfiguration.ComplianceRecordingPolicyName;
            this.BlobServiceEndpoint = azureConfiguration.BlobServiceEndpoint;
            this.StorageAccountName = azureConfiguration.StorageAccountName;
            this.StorageAccountKey = azureConfiguration.StorageAccountKey;
            this.MediaPlatformSettings = azureConfiguration.MediaPlatformSettings;
            // Initialize Audio Settings
            this.AudioSettings = new AudioSettings
            {
                WavSettings = (WAVSampleRate > 0) ? new WAVSettings(WAVSampleRate, WAVQuality) : null
            };
        }
    }
}