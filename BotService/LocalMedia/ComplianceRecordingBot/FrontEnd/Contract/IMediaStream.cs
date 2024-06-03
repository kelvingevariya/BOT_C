using Microsoft.Graph.Communications.Calls;
using Microsoft.Skype.Bots.Media;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComplianceRecordingBot.FrontEnd.Contract
{
    /// <summary>
    /// Interface IMediaStream
    /// </summary>
    public interface IMediaStream
    {
        /// <summary>
        /// Appends the audio buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="participant">The participant.</param>
        /// <returns>Task.</returns>
        Task AppendAudioBuffer(AudioMediaBuffer buffer, List<IParticipant> participant);

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        Task AudioEnd();

        /// <summary>
        /// Appends the video buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="participant">The participant.</param>
        /// <returns></returns>
        Task AppendVideoBuffer(VideoMediaBuffer buffer, List<IParticipant> participant);

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        Task VideoEnd();

        /// <summary>
        /// Appends the vbss buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="participant">The participant.</param>
        /// <returns></returns>
        Task AppendVBSSBuffer(VideoMediaBuffer buffer, List<IParticipant> participant);

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        Task VBSSEnd();
    }
}