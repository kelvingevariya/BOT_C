﻿using Microsoft.Graph.Communications.Calls;
using System.Collections.Generic;

namespace ComplianceRecordingBot.FrontEnd.Models
{
    /// <summary>
    /// Class ParticipantData.
    /// </summary>
    public class ParticipantData
    {
        /// <summary>
        /// Gets or sets the added resources.
        /// </summary>
        /// <value>The added resources.</value>
        public ICollection<IParticipant> AddedResources { get; set; }

        /// <summary>
        /// Gets or sets the removed resources.
        /// </summary>
        /// <value>The removed resources.</value>
        public ICollection<IParticipant> RemovedResources { get; set; }
    }
}