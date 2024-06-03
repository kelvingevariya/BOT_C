using System;

namespace GraphApiTools
{
    /// <summary>
    ///
    /// </summary>
    public class OnlineMeetingRequestModel
    {
        /// <summary>
        ///
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string AgentUPN { get; set; }

        /// <summary>
        ///
        /// </summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>
        ///
        /// </summary>
        public DateTime EndDateTime { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string MeetingId { get; set; }
    }
}