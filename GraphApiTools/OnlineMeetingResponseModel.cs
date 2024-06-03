namespace GraphApiTools
{
    /// <summary>
    ///
    /// </summary>
    public class OnlineMeetingResponseModel
    {
        /// <summary>
        ///
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string JoinWebUrl { get; set; }

        /// <summary>
        /// Teams Meeting ID
        /// </summary>
        public string MeetingId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string MeetingSubject { get; set; }
    }
}