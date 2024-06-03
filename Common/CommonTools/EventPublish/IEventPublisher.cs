namespace CommonTools.EventPublish
{
    /// <summary>
    /// Interface IEventPublisher
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes the specified subject.
        /// event string formatting required for topic endpoint -- topic name and region name
        /// e.g. https://[YOUR-TOPIC-NAME].[REGION-NAME]-1.eventgrid.azure.net/api/events"
        /// </summary>
        /// <param name="TopicEndpoint">https://{0}.{1}-1.eventgrid.azure.net/api/events</param>
        /// <param name="Subject">The subject.</param>
        /// <param name="Message">The message.</param>
        /// <param name="TopicName">Name of the topic.</param>
        void Publish(string TopicEndpoint, string Subject, string Message, string TopicName = "");
    }
}