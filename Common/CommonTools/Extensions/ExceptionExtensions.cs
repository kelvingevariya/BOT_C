namespace CommonTools.Extensions
{
    using Microsoft.Graph;
    using System;
    using System.Net;
    using System.Net.Http;

    /// <summary>
    /// Extension methods for Exception.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Inspect the exception type/error and return the correct response.
        /// </summary>
        /// <param name="exception">The caught exception.</param>
        /// <returns>The <see cref="HttpResponseMessage"/>.</returns>
        public static HttpResponseMessage InspectExceptionAndReturnResponse(this Exception exception)
        {
            HttpResponseMessage responseToReturn;
            if (exception is ServiceException e)
            {
                responseToReturn = (int)e.StatusCode >= 200
                    ? new HttpResponseMessage(e.StatusCode)
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError);
                if (e.ResponseHeaders != null)
                {
                    foreach (var responseHeader in e.ResponseHeaders)
                    {
                        responseToReturn.Headers.TryAddWithoutValidation(responseHeader.Key, responseHeader.Value);
                    }
                }
                responseToReturn.Content = new StringContent(e.ToString());
            }
            else
            {
                responseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(exception.ToString()),
                };
            }
            return responseToReturn;
        }
    }
}