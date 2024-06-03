namespace ComplianceRecordingBot.FrontEnd
{
    /// <summary>
    /// Class BotConstants.
    /// </summary>
    public static class BotConstants
    {
        /// <summary>
        /// The number of multiview sockets
        /// </summary>
        public const uint NumberOfMultiviewSockets = 9;

        /// <summary>
        /// The default sample rate
        /// </summary>
        public const int DefaultSampleRate = 16000;

        /// <summary>
        /// The default bits
        /// </summary>
        public const int DefaultBits = 16;

        /// <summary>
        /// The default channels
        /// </summary>
        public const int DefaultChannels = 1;

        /// <summary>
        /// The highest sampling quality level
        /// </summary>
        public const int HighestSamplingQualityLevel = 60;

        /// <summary>
        ///
        /// </summary>
        public const string AzureStorageContainerName = "meetingmapping";
    }
}