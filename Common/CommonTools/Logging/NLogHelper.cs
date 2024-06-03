namespace CommonTools.Logging
{
    using NLog;

    /// <summary>
    /// NLogHelper.
    /// </summary>
    public static class NLogHelper
    {
        /// <summary>
        ///
        /// </summary>
        public static ILogger Instance { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public static void SetupNLog()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var target = new NLog.Targets.FileTarget("f")
            {
                FileName = "D:\\teams-recording-bot\\nlogs\\${shortdate}.log",
                Layout = "${date:format=yyyy-MM-dd HH\\:mm\\:ss} ${message}",
                MaxArchiveFiles = 50,
                ArchiveAboveSize = 1024 * 1024 * 10,
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, target);
            LogManager.Configuration = config;
            Instance = LogManager.GetLogger("debug");
        }
    }
}