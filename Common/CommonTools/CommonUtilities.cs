namespace CommonTools
{
    using Microsoft.Graph.Communications.Common.Telemetry;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The utility class.
    /// </summary>
    public static class CommonUtilities
    {
        /// <summary>
        /// WriteFile.
        /// </summary>
        /// <param name="destFilePath">destFilePath.</param>
        /// <param name="content">content.</param>
        /// <param name="delExistsFile">delExistsFile.</param>
        /// <returns>bool.</returns>
        public static bool WriteFile(string destFilePath, string content, bool delExistsFile = true)
        {
            bool result;
            try
            {
                if (File.Exists(destFilePath) && delExistsFile)
                {
                    File.Delete(destFilePath);
                }
                File.WriteAllText(destFilePath, content, Encoding.UTF8);
                result = true;
            }
            catch (Exception ex)
            {
                throw;
            }
            return result;
        }

        /// <summary>
        /// ReadFile.
        /// </summary>
        /// <param name="filePath">filePath.</param>
        /// <returns>string.</returns>
        public static string ReadFile(string filePath)
        {
            string result = string.Empty;
            try
            {
                if (File.Exists(filePath))
                {
                    result = File.ReadAllText(filePath, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            return result;
        }

        /// <summary>
        /// ToByteArray.
        /// </summary>
        /// <param name="input">input.</param>
        /// <returns>byte[].</returns>
        public static byte[] ToByteArray(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// .
        /// </summary>
        /// <param name="existList">existList.</param>
        /// <param name="maxValue">maxValue.</param>
        /// <returns>uint.</returns>
        public static uint GetRandNum(List<uint> existList, int maxValue)
        {
            uint result = 0;
            Random rd = new Random(Guid.NewGuid().GetHashCode());
            int rand_num = rd.Next(0, maxValue);
            result = Convert.ToUInt32(rand_num);
            while (existList.Any(i => i == result))
            {
                rand_num = rd.Next(0, maxValue);
                result = Convert.ToUInt32(rand_num);
            }
            existList.Add(result);
            return result;
        }

        /// <summary>
        /// Extension for Task to execute the task in background and log any exception.
        /// </summary>
        /// <param name="task">Task to execute and capture any exceptions.</param>
        /// <param name="logger">Graph logger.</param>
        /// <param name="description">Friendly description of the task for debugging purposes.</param>
        /// <param name="memberName">Calling function.</param>
        /// <param name="filePath">File name where code is located.</param>
        /// <param name="lineNumber">Line number where code is located.</param>
        /// <returns>
        /// A <see cref="Task" /> representing the asynchronous operation.
        /// </returns>
        public static async Task ForgetAndLogExceptionAsync(
            this Task task,
            IGraphLogger logger,
            string description = null,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                await task.ConfigureAwait(false);
                logger?.Verbose(
                    $"Completed running task successfully: {description ?? string.Empty}",
                    memberName: memberName,
                    filePath: filePath,
                    lineNumber: lineNumber);
            }
            catch (Exception e)
            {
                // Log and absorb all exceptions here.
                logger.Error(
                    e,
                    $"Caught an Exception running the task: {description ?? string.Empty} {e.Message}\n StackTrace: {e.StackTrace}",
                    memberName: memberName,
                    filePath: filePath,
                    lineNumber: lineNumber);
            }
        }

        /// <summary>
        /// ForgetAndNLogExceptionAsync.
        /// </summary>
        /// <param name="task">task.</param>
        /// <param name="logger">logger.</param>
        /// <param name="description">description.</param>
        /// <returns>Task.</returns>
        public static async Task ForgetAndNLogExceptionAsync(this Task task, ILogger logger, string description = null)
        {
            try
            {
                await task.ConfigureAwait(false);
                logger?.Debug($"Completed running task successfully: {description ?? string.Empty}");
            }
            catch (Exception e)
            {
                // Log and absorb all exceptions here.
                logger.Debug($"Caught an error running task: {description ?? string.Empty} {e.Message}\n StackTrace: {e.StackTrace}");
            }
        }
    }
}