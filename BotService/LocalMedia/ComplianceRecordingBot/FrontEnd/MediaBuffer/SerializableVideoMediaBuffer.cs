using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ComplianceRecordingBot.FrontEnd.MediaBuffer
{
    /// <summary>
    /// Class SerializableVideoMediaBuffer.
    /// Implements the <see cref="System.IDisposable" />
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class SerializableVideoMediaBuffer : IDisposable
    {
        /// <summary>
        /// The length of data in the media buffer.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// Stride of the video buffer. This property is optional when sourcing video buffers
        /// that are sent via the IVideoSocket.Send API. Stride (also called pitch) represents
        /// the number of bytes it takes to read one row of pixels in memory. It may differ
        /// from the width depending on the color format.
        /// </summary>
        public int Stride { get; set; }

        /// <summary>
        /// MediaSourceId (MSI) of the video buffer. Within group or conference video calls,
        /// the MSI value identifies the video media source. This property is populated by
        /// the Real-Time Media Platform for Bots on received video buffers. When sending
        /// buffers via the IVideoSocket.Send API, this property is unused.
        /// </summary>
        public uint MediaSourceId { get; set; }

        /// <summary>
        /// Timestamp of when the media content was sourced, in 100-ns units. When sourcing media buffers,
        /// this property should be set using the value from the MediaPlatform.GetCurrentTimestamp() API
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the buffer.
        /// </summary>
        /// <value>The buffer.</value>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// The participants
        /// </summary>
        private List<IParticipant> participants;

        /// <summary>
        /// Gets or sets the Participant ID
        /// </summary>
        public string ParticipantID { get; set; }

        /// <summary>
        /// Gets or sets the ad identifier.
        /// </summary>
        /// <value>The ad identifier.</value>
        public string AdId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableVideoMediaBuffer" /> class.
        /// </summary>
        public SerializableVideoMediaBuffer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableVideoMediaBuffer" /> class.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="participants"></param>
        public SerializableVideoMediaBuffer(VideoMediaBuffer buffer, List<IParticipant> participants)
        {
            this.participants = participants;
            Length = buffer.Length;
            Stride = buffer.Stride;
            MediaSourceId = buffer.MediaSourceId;
            Timestamp = buffer.Timestamp;
            Width = buffer.VideoFormat.Width;
            Height = buffer.VideoFormat.Height;
            var participant = GetParticipantFromMSI(MediaSourceId);
            ParticipantID = participant?.Id;
            var i = GetParticipantIdentity(participant);
            if (i != null)
            {
                AdId = i.Id;
            }
            else
            {
                AdId = participant?.Resource?.Info?.Identity?.User?.Id;
            }
            if (Length > 0)
            {
                Buffer = new byte[Length];
                Marshal.Copy(buffer.Data, Buffer, 0, (int)Length);
            }
        }

        /// <summary>
        /// Gets the participant from msi.
        /// </summary>
        /// <param name="msi">The msi.</param>
        /// <returns>IParticipant.</returns>
        private IParticipant GetParticipantFromMSI(uint msi)
        {
            return this.participants.SingleOrDefault(x => x.Resource.IsInLobby == false && x.Resource.MediaStreams.Any(y => y.SourceId == msi.ToString()));
        }

        /// <summary>
        /// Get the participant Identity.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns>Identity.</returns>
        private Identity GetParticipantIdentity(IParticipant p)
        {
            if (p?.Resource?.Info?.Identity?.AdditionalData != null)
            {
                foreach (var i in p.Resource.Info.Identity.AdditionalData)
                {
                    if (i.Key != "applicationInstance" && i.Value is Identity)
                    {
                        return i.Value as Identity;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Buffer = null;
        }
    }
}