using System;
using System.Configuration;

namespace DownloaderApp
{
    public class AppConfiguration
    {
        /// <summary>
        ///
        /// </summary>
        public string BotName { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string AadAppId { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string AadAppSecret { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public string TenantId { get; private set; }

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

        /// <summary>
        ///
        /// </summary>
        public string DownloadDirPath { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public int MaxProcessCount { get; private set; }

        public AppConfiguration()
        {
            Initial();
        }

        private void Initial()
        {
            BotName = ConfigurationManager.AppSettings["BotName"].ToString();
            AadAppId = ConfigurationManager.AppSettings["AadAppId"].ToString();
            AadAppSecret = ConfigurationManager.AppSettings["AadAppSecret"].ToString();
            TenantId = ConfigurationManager.AppSettings["TenantId"].ToString();
            GroupName = ConfigurationManager.AppSettings["GroupName"].ToString();
            HostName = ConfigurationManager.AppSettings["HostName"].ToString();
            RelativePath = ConfigurationManager.AppSettings["RelativePath"].ToString();
            DownloadDirPath = ConfigurationManager.AppSettings["DownloadDirPath"].ToString();
            MaxProcessCount = Convert.ToInt32(ConfigurationManager.AppSettings["MaxProcessCount"].ToString());
        }
    }
}