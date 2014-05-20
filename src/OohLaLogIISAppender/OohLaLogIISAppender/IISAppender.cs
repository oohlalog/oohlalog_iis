using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using System.Configuration;
namespace OohLaLog
{
    public class IISAppender :IHttpModule 
    {

        private static string DefaultHost = "api.oohlalog.com";
        private static bool DefaultIsSecure = false;
        private static string DefaultUrlPath = "api/logging/save.json";
        private enum LogLevels {ALL=0,DEBUG=1,INFO=2,WARN=3,ERROR=4};
        private static LogLevels DefaultLogLevel = LogLevels.ALL;
        /*
         * TODO: 
         * 1) Implement buffering
         * 2) configurable HttpStatusCodeRanges to configure any code to any log level
         * 3) configurable request/response data that can be logged
         */
        #region IHttpModule Methods
        void IHttpModule.Init(HttpApplication context)
        {
            lock (this)
            {
                NameValueCollection ollConfig = (NameValueCollection)ConfigurationManager.GetSection("oohlalog");
                if (ollConfig == null)
                    return;
                ApiKey = ollConfig["apikey"];
                HostName = ollConfig["hostname"] ?? System.Environment.MachineName;
                Host = ollConfig["host"] ?? DefaultHost;
                IsSecure = ((string)ollConfig["issecure"] ?? DefaultIsSecure.ToString()).ToLower() == "true";
                LogLevels ll;
                if (!Enum.TryParse((string)ollConfig["loglevel"] ?? DefaultLogLevel.ToString(), out ll))
                    ll = LogLevels.ALL;
                LogLevel = ll;
                if (!String.IsNullOrEmpty(ApiKey))
                {
                    context.EndRequest += new EventHandler(context_EndRequest);
                    Url = String.Format("{0}://{1}/{2}?apiKey={3}", (IsSecure ? "https" : "http"), Host
                        , DefaultUrlPath, ApiKey);
                    LogCodes = new List<HttpStatusCodeRange>();
                    if (LogLevel <= LogLevels.INFO)
                        LogCodes.Add(new HttpStatusCodeRange() { MinCode = 100, MaxCode = 399, LogType = LogLevels.INFO });
                    if (LogLevel <= LogLevels.WARN)
                        LogCodes.Add(new HttpStatusCodeRange() { MinCode = 400, MaxCode = 499, LogType = LogLevels.WARN });
                    if (LogLevel <= LogLevels.ERROR)
                        LogCodes.Add(new HttpStatusCodeRange() { MinCode = 500, MaxCode = 599, LogType = LogLevels.ERROR });
                    if (LogLevel <= LogLevels.DEBUG)
                    {
                        sendPayload(BuildJsonString(LogLevels.DEBUG.ToString(),
                            String.Format("IIS Appender Initialized With Following Parameters: HostName={0}; ApiKey={1}; OllUrl={2}; LogLevel={3};",
                            HostName, ApiKey, Url, LogLevel.ToString()
                            )
                        ));
                    }
                }
                else
                {
                    sendPayload(BuildJsonString("ERROR", "IIS Appender Initialization Error: No ApiKey Configured"));
                }
            }
        }
        public void Dispose()
        {
            //do nothing for now
        }
        #endregion

        #region Properties
        string HostName { get; set; }
        string Url { get; set; }
        string ApiKey { get; set; }
        string Host { get; set; }
        bool IsSecure { get; set; }
        LogLevels LogLevel { get; set; }
        List<HttpStatusCodeRange> LogCodes { get; set; }
        #endregion

        void context_EndRequest(object sender, EventArgs e)
        {
            HttpApplication httpApplication = (HttpApplication)sender;
            int code = httpApplication.Response.StatusCode;
            var x = LogCodes.Find(c => code >= c.MinCode && code <= c.MaxCode);
            if (x != null)
                sendPayload(BuildJsonString(x.LogType.ToString(), httpApplication.Context));
        }
        #region Helper Methods
        private void sendPayload(string payload)
        {
            try
            {
                using (WebClient client = new MyWebClient())
                {
                    client.Headers.Add("Content-Type", "application/json");
                    string response = client.UploadString(Url, String.Format("{{\"logs\": [{1}]}}", HostName, payload));
                }
            }
            catch (Exception e)
            {
        //        do nothing for now
            }
        }
        private string GetLogMessage(HttpContext context)
        {   
            return string.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9:0}",
                context.Timestamp.ToString("yyyy-MM-dd"),
                context.Timestamp.ToString("HH:mm:ss"),
                context.Request.HttpMethod,
                context.Request.Path,
                GetQueryString(context),
                GetUserAgent(context),
                GetUser(context),
                GetIPAddress(context) ?? "-",
                context.Response.StatusCode,
                new TimeSpan(DateTime.Now.Ticks - context.Timestamp.Ticks).TotalMilliseconds
                );
        }
        private string GetUserAgent(HttpContext context)
        {
            if (String.IsNullOrEmpty(context.Request.UserAgent))
                return "-";
            else
                return context.Request.UserAgent.Replace(" ", "+");
        }
        private string GetQueryString(HttpContext context)
        {
            if (context.Request.QueryString == null || context.Request.QueryString.Count == 0)
                return "-";
            else
            {
                return context.Request.RawUrl.Substring(context.Request.Path.Length+1);
            }
        }
        private string GetUser(HttpContext context)
        {
            if (context.User == null || context.User.Identity == null || String.IsNullOrEmpty(context.User.Identity.Name))
                return "-";
            else
                return context.User.Identity.Name.Replace(" ", "+");
        }
        private string GetIPAddress(HttpContext context)
        {
            string ipAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length != 0)
                {
                    return addresses[0];
                }
            }

            return context.Request.ServerVariables["REMOTE_ADDR"];
        }
        private string BuildJsonString(string logType, string message)
        {
            if (message == null)
                return "";

            return String.Format("{{\"hostName\": \"{3}\",\"message\":\"{0}\",\"level\":\"{1}\",\"timestamp\":{2}}}",
                        HttpUtility.JavaScriptStringEncode(message),
                        logType,
                        DateTimeToEpochTime(DateTime.UtcNow).ToString(),
                        HostName);
        }
        private string BuildJsonString(string logType, HttpContext context)
        {
            if (context == null)
                return "";
            
            return String.Format("{{\"hostName\": \"{3}\",\"message\":\"{0}\",\"level\":\"{1}\",\"timestamp\":{2}}}",
                        HttpUtility.JavaScriptStringEncode(GetLogMessage(context)),
                        logType,
                        DateTimeToEpochTime(context.Timestamp.ToUniversalTime()).ToString(),
                        HostName);
        }
        private static long DateTimeToEpochTime(DateTime utc)
        {
            long m_epochReferenceTimeTicks = (new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).Ticks;
            return Convert.ToInt64((new TimeSpan(utc.Ticks - m_epochReferenceTimeTicks)).TotalMilliseconds);
        }
        #endregion

        #region Helper Classes
        private class HttpStatusCodeRange 
        {
            public short MinCode;
            public short MaxCode;
            public LogLevels LogType; 
        }
        private class MyWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = 1000;
                return w;
            }
        }
        #endregion
    }

}
