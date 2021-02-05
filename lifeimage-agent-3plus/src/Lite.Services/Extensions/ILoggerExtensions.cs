using Lite.Core.Guard;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace Lite.Services
{
    public static class ILoggerExtensions
    {
        public static void LogHeaders(this ILogger logger, HttpResponseMessage response, string taskInfo)
        {
            Throw.IfNull(logger);
            Throw.IfNull(response);

            foreach (var header in response.Headers)
            {
                foreach (var value in header.Value)
                {
                    logger.Log(LogLevel.Debug, $"{taskInfo} response.Headers: {header.Key} {value}");
                }
            }
        }

        public static void LogHttpResponseAndHeaders(this ILogger logger, HttpResponseMessage response, string taskInfo)
        {
            logger.LogHttpResponse(response, taskInfo);
            logger.LogHeaders(response, taskInfo);
        }

        public static void LogHttpResponse(this ILogger logger, HttpResponseMessage response, string taskInfo)
        {
            Throw.IfNull(logger);
            Throw.IfNull(response);

            logger.Log(LogLevel.Debug, $"{taskInfo} result: {response.Version} {response.StatusCode} {response.ReasonPhrase}");
        }

        public static void LogCookies(this ILogger logger, CookieCollection cookies, string taskInfo = null)
        {
            Throw.IfNull(logger);
            Throw.IfNull(cookies);

            foreach (var cookie in cookies)
            {
                if (taskInfo == null)
                {
                    logger.Log(LogLevel.Warning, $"Cookie: {cookie}");
                }
                else
                {
                    logger.Log(LogLevel.Debug, $"{taskInfo} Cookie: {cookie}");
                }
            }
        }

        public static void LogFullException(this ILogger logger, Exception e, string msg = null)
        {
            string logMsg;
            if (string.IsNullOrEmpty(msg))
            {
                logMsg = $"{e.Message} {e.StackTrace}";
            }
            else
            {
                logMsg = $"{msg}. {e.Message} {e.StackTrace}";
            }

            logger.Log(LogLevel.Critical, e, logMsg);

            if (e.InnerException != null)
            {
                logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
            }
        }
    }
}
