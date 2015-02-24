using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.ApplicationInsights;
using log4net.Appender;
using log4net.Core;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace ApplicationInsights.Log4Net
{
    public class InsightsAppender : AppenderSkeleton
    {
        public bool Enabled { get; private set; }

        private TelemetryClient _telemetryClient;

        /// <summary>
        /// Get/set The Application Insights instrumentationKey for your application.
        /// 
        /// </summary>
        /// 
        /// <remarks>
        /// This is normally pushed from when Appender is being initialized.
        /// 
        /// </remarks>
        public string InstrumentationKey { get; set; }

        /// <summary>
        /// The <see cref="T:Microsoft.ApplicationInsights.Log4NetAppender.ApplicationInsightsAppender"/> requires a layout.
        ///             This Appender converts the LoggingEvent it receives into a text string and requires the layout format string to do so.
        /// 
        /// </summary>
        protected new virtual bool RequiresLayout
        {
            get
            {
                return true;
            }
        }

        public override void ActivateOptions()
        {
            Enabled = !string.IsNullOrEmpty(InstrumentationKey);
            if (Enabled)
            {
                _telemetryClient = new TelemetryClient
                {
                    Context =
                    {
                        InstrumentationKey = InstrumentationKey
                    }
                };
            }
            else
            {
                Threshold = Level.Off;
                System.Diagnostics.Debugger.Log(1, "InsightsLog4Net", "Insights Log4Net Appender is not enabled.  No InstrumentationKey was specified.");
            }
            base.ActivateOptions();
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            try
            {
                if (!Enabled) return;

                CleanContext(_telemetryClient);
                var msg = loggingEvent.RenderedMessage ?? "Log4Net";

                ITelemetry telemetry;
                if (loggingEvent.Level == Level.Fatal || loggingEvent.Level == Level.Error)
                    telemetry = new ExceptionTelemetry(loggingEvent.ExceptionObject ?? new Exception(string.Format("{0} - {1}", loggingEvent.Level, msg)));
                else
                    telemetry = new TraceTelemetry(string.Format("{0} - {1}", loggingEvent.Level, msg));

                BuildCustomProperties(loggingEvent, telemetry);
                BuildClientContext(_telemetryClient, loggingEvent);
                _telemetryClient.Track(telemetry);
            }
            catch (ArgumentNullException ex)
            {
                throw new LogException(ex.Message, ex);
            }
        }

        private void CleanContext(TelemetryClient client)
        {
            client.Context.Device.Id = null;
            client.Context.Device.Language = null;
            client.Context.User.AccountId = null;
            client.Context.User.Id = null;
            client.Context.User.UserAgent = null;
            client.Context.Session.Id = null;
        }


        private void BuildClientContext(TelemetryClient client, LoggingEvent loggingEvent)
        {
            client.Context.Device.Id = Environment.MachineName;
            client.Context.Device.Language = Thread.CurrentThread.CurrentCulture.EnglishName;            
            client.Context.User.AccountId = loggingEvent.UserName;
            client.Context.User.Id = loggingEvent.UserName;
            
            var properties = loggingEvent.Properties;
            foreach (var key in properties.GetKeys())
            {
                var k = key.ToLower().Replace("-","");
                var value = properties[key].ToString();
                if (k == "useragent") client.Context.User.UserAgent = value;
                if (k == "sessionid") client.Context.Session.Id = value;
                if (k == "userid" || k == "user")
                {
                    client.Context.User.AccountId = value;
                    client.Context.User.Id = value;
                }
            }
            foreach (var key in log4net.ThreadContext.Properties.GetKeys())
            {
                var k = key.ToLower().Replace("-", "");
                var value = log4net.ThreadContext.Properties[key].ToString();
                if (k == "useragent") client.Context.User.UserAgent = value;
                if (k == "sessionid") client.Context.Session.Id = value;
                if (k == "userid")
                {
                    client.Context.User.AccountId = value;
                    client.Context.User.Id = value;
                }
            }
        }

        private static void AddLoggingEventProperty(string key, string value, IDictionary<string, string> metaData)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;

            if (!metaData.ContainsKey(key))
                metaData.Add(key, value);
            else
                metaData[key] = value;
        }



        private void BuildCustomProperties(LoggingEvent loggingEvent, ITelemetry trace)
        {
            IDictionary<string, string> properties = trace.Context.Properties;

            properties.Add("SourceType", "Log4Net");
            AddLoggingEventProperty("LoggerName", loggingEvent.LoggerName, properties);
            AddLoggingEventProperty("LoggingLevel", loggingEvent.Level.ToString(), properties);
            AddLoggingEventProperty("ThreadName", loggingEvent.ThreadName, properties);
            AddLoggingEventProperty("TimeStamp", loggingEvent.TimeStamp.ToString(CultureInfo.InvariantCulture), properties);
            AddLoggingEventProperty("UserName", loggingEvent.UserName, properties);
            AddLoggingEventProperty("UserId", loggingEvent.UserName, properties);
            AddLoggingEventProperty("Domain", loggingEvent.Domain, properties);
            AddLoggingEventProperty("Identity", loggingEvent.Identity, properties);

            foreach (var p in log4net.ThreadContext.Properties.GetKeys())
            {
                AddLoggingEventProperty(p, log4net.ThreadContext.Properties[p].ToString(), properties);
            }
            
            var locationInformation = loggingEvent.LocationInformation;
            if (locationInformation != null)
            {
                AddLoggingEventProperty("ClassName", locationInformation.ClassName, properties);
                AddLoggingEventProperty("FileName", locationInformation.FileName, properties);
                AddLoggingEventProperty("MethodName", locationInformation.MethodName, properties);
                AddLoggingEventProperty("LineNumber", locationInformation.LineNumber, properties);
            }
            
            if (loggingEvent.ExceptionObject != null)
            {
                AddLoggingEventProperty("ExceptionMessage", loggingEvent.ExceptionObject.Message, properties);
                var sb = new StringBuilder();

                foreach (var o in loggingEvent.ExceptionObject.Data)
                {
                    sb.Append(string.Format("{0}\r\n", o));
                }

                var data = sb.ToString();
                if (!string.IsNullOrEmpty(data))
                {
                    AddLoggingEventProperty("ExceptionData", data, properties);
                }
                
                if (loggingEvent.ExceptionObject.TargetSite != null)
                {
                    var site = string.Format("{0}.{1}", loggingEvent.ExceptionObject.TargetSite.ReflectedType, loggingEvent.ExceptionObject.TargetSite.Name);
                    AddLoggingEventProperty("ExceptionTargetSite", site, properties);
                }

                if (loggingEvent.ExceptionObject.Source != null) AddLoggingEventProperty("ExceptionSource", loggingEvent.ExceptionObject.Source, properties);

                if (loggingEvent.ExceptionObject.StackTrace != null) AddLoggingEventProperty("ExceptionStackTrace", loggingEvent.ExceptionObject.StackTrace, properties);

                AddLoggingEventProperty("ExceptionHResult", loggingEvent.ExceptionObject.HResult.ToString(), properties);

                if (string.IsNullOrEmpty(loggingEvent.ExceptionObject.HelpLink))
                {
                    AddLoggingEventProperty("ExceptionHelp", string.Format("{0}{1}", "https://social.msdn.microsoft.com/Search/en-US?query=", loggingEvent.ExceptionObject.GetType().FullName), properties);
                }
                else
                {
                    AddLoggingEventProperty("ExceptionHelp", loggingEvent.ExceptionObject.HelpLink, properties);    
                }
            }

            if (loggingEvent.MessageObject != null)
            {
                AddLoggingEventProperty("Message", loggingEvent.MessageObject.ToString(), properties);    
            }
        }
    }
}
