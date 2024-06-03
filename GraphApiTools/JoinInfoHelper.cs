using Microsoft.Graph;
using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace GraphApiTools
{
    /// <summary>
    /// Gets the join information.
    /// </summary>
    public class JoinInfoHelper
    {
        /// <summary>
        /// Parse Join URL into its components.
        /// </summary>
        /// <param name="joinURL">Join URL from Team's meeting body.</param>
        /// <returns>Parsed data.</returns>
        public static (ChatInfo, MeetingInfo) ParseJoinURL(string joinURL)
        {
            if (string.IsNullOrEmpty(joinURL))
            {
                throw new ArgumentException($"Join URL cannot be null or empty: {joinURL}", nameof(joinURL));
            }
            var decodedURL = WebUtility.UrlDecode(joinURL);
            // URL being needs to be in this format.
            var regex = new Regex("https://teams\\.microsoft\\.com.*/(?<thread>[^/]+)/(?<message>[^/]+)\\?context=(?<context>{.*})");
            var match = regex.Match(decodedURL);

            if (!match.Success)
                throw new ArgumentException($"Join URL cannot be parsed: {joinURL}.", nameof(joinURL));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(match.Groups["context"].Value)))
            {
                var ctxt = (Context)new DataContractJsonSerializer(typeof(Context)).ReadObject(stream);
                var chatInfo = new ChatInfo
                {
                    ThreadId = match.Groups["thread"].Value,
                    MessageId = match.Groups["message"].Value,
                    ReplyChainMessageId = ctxt.MessageId,
                };
                var meetingInfo = new OrganizerMeetingInfo
                {
                    Organizer = new IdentitySet
                    {
                        User = new Identity { Id = ctxt.Oid },
                    }
                };
                //meetingInfo.Organizer.SetApplicationInstance(new Identity { Id = ctxt.Oid });
                meetingInfo.Organizer.User.SetTenantId(ctxt.Tid);
                return (chatInfo, meetingInfo);
            }
        }

        /// <summary>
        /// Join URL context.
        /// </summary>
        [DataContract]
        private class Context
        {
            /// <summary>
            /// Gets or sets the Tenant Id.
            /// </summary>
            [DataMember]
            public string Tid { get; set; }

            /// <summary>
            /// Gets or sets the AAD object id of the user.
            /// </summary>
            [DataMember]
            public string Oid { get; set; }

            /// <summary>
            /// Gets or sets the chat message id.
            /// </summary>
            [DataMember]
            public string MessageId { get; set; }
        }
    }
}