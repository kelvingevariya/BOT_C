namespace CommonTools.EventPublish
{
    using Microsoft.Azure.EventGrid;
    using Microsoft.Azure.EventGrid.Models;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Class EventGridPublisher.
    /// Implements the <see cref="IEventPublisher" />
    /// </summary>
    /// <seealso cref="IEventPublisher" />
    public class EventGridPublisher : IEventPublisher
    {
        /// <summary>
        /// The topic name
        /// </summary>
        private string topicName = "recordingbotevents";

        /// <summary>
        /// The region name
        /// </summary>
        private string regionName = string.Empty;

        /// <summary>
        /// The topic key
        /// </summary>
        private string topicKey = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridPublisher" /> class.
        /// </summary>
        /// <param name="TopicName"></param>
        /// <param name="TopicKey"></param>
        /// <param name="RegionName"></param>
        public EventGridPublisher(string TopicName, string TopicKey, string RegionName)
        {
            this.topicName = TopicName;
            this.topicKey = TopicKey;
            this.regionName = RegionName;
        }

        /// <summary>
        /// Publishes the specified subject.
        /// event string formatting required for topic endpoint -- topic name and region name
        /// e.g. https://[YOUR-TOPIC-NAME].[REGION-NAME]-1.eventgrid.azure.net/api/events"
        /// </summary>
        /// <param name="TopicEndpoint">https://{0}.{1}-1.eventgrid.azure.net/api/events</param>
        /// <param name="Subject">The subject.</param>
        /// <param name="Message">The message.</param>
        /// <param name="TopicName">Name of the topic.</param>
        public void Publish(string TopicEndpoint, string Subject, string Message, string TopicName)
        {
            if (TopicName.Length == 0)
            {
                TopicName = this.topicName;
            }
            var topicEndpoint = String.Format(TopicEndpoint, TopicName, this.regionName);
            var topicKey = this.topicKey;
            if (topicKey?.Length > 0)
            {
                var topicHostname = new Uri(topicEndpoint).Host;
                var topicCredentials = new TopicCredentials(topicKey);
                var client = new EventGridClient(topicCredentials);
                // Add event to list
                var eventsList = new List<EventGridEvent>();
                ListAddEvent(eventsList, Subject, Message);
                // Publish
                client.PublishEventsAsync(topicHostname, eventsList).GetAwaiter().GetResult();
                if (Subject.StartsWith("CallTerminated"))
                    Console.WriteLine($"Publish to {TopicName} subject {Subject} message {Message}");
                else
                    Console.WriteLine($"Publish to {TopicName} subject {Subject}");
            }
            else
                Console.WriteLine($"Skipped publishing {Subject} events to Event Grid topic {TopicName} - No topic key specified");
        }

        /// <summary>
        /// Lists the add event.
        /// </summary>
        /// <param name="eventsList">The events list.</param>
        /// <param name="Subject">The subject.</param>
        /// <param name="Message">The message.</param>
        /// <param name="DataVersion">The data version.</param>
        private static void ListAddEvent(List<EventGridEvent> eventsList, string Subject, string Message, string DataVersion = "2.0")
        {
            eventsList.Add(new EventGridEvent()
            {
                Id = Guid.NewGuid().ToString(),
                EventType = "RecordingBot.BotEventData",
                Data = new BotEventData()
                {
                    Message = Message
                },
                EventTime = DateTime.Now,
                Subject = Subject,
                DataVersion = DataVersion
            });
        }
    }
}