using Newtonsoft.Json;

namespace CommonTools.EventPublish
{
    /// <summary>
    /// Class BotEventData.
    /// </summary>
    public class BotEventData
    {
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>The message.</value>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}