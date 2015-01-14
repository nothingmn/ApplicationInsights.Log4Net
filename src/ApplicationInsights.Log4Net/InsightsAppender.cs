using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
        public new virtual void ActivateOptions()
        {
            base.ActivateOptions();            
        }


        protected override void Append(LoggingEvent loggingEvent)
        {
            try
            {
                if (!string.IsNullOrEmpty(InstrumentationKey))
                {
                    var msg = loggingEvent.RenderedMessage ?? "Log4Net";
                    var telemetryClient = new TelemetryClient();
                    telemetryClient.Context.InstrumentationKey = InstrumentationKey;
                    ITelemetry telemetry = null;
                    if (loggingEvent.Level == Level.Fatal || loggingEvent.Level == Level.Error)
                    {
                        telemetry = new ExceptionTelemetry(loggingEvent.ExceptionObject ?? new Exception(string.Format("{0} - {1}", loggingEvent.Level, msg)));
                    }
                    else
                    {
                        telemetry = new TraceTelemetry(string.Format("{0} - {1}", loggingEvent.Level, msg));
                    }


                    this.BuildCustomProperties(loggingEvent, telemetry);
                    BuildClientContext(telemetryClient, loggingEvent);
                    telemetryClient.Track(telemetry);
                }
            }
            catch (ArgumentNullException ex)
            {
                throw new LogException(ex.Message, (Exception)ex);
            }
        }


        private void BuildClientContext(TelemetryClient client, LoggingEvent loggingEvent)
        {
            client.Context.Device.Id = System.Environment.MachineName;
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
            if (value == null)
                return;
            metaData.Add(key, value);
        }



        private void BuildCustomProperties(LoggingEvent loggingEvent, ITelemetry trace)
        {
            IDictionary<string, string> properties = trace.Context.Properties;

            properties.Add("source-type", "Log4Net");
            AddLoggingEventProperty("logger-name", loggingEvent.LoggerName, properties);
            AddLoggingEventProperty("logging-level", loggingEvent.Level.ToString(), properties);
            AddLoggingEventProperty("thread-name", loggingEvent.ThreadName, properties);
            AddLoggingEventProperty("time-stamp", loggingEvent.TimeStamp.ToString((IFormatProvider)CultureInfo.InvariantCulture), properties);

            foreach (var p in log4net.ThreadContext.Properties.GetKeys())
            {
                properties.Add(p, log4net.ThreadContext.Properties[p].ToString());
            }


            LocationInfo locationInformation = loggingEvent.LocationInformation;
            if (locationInformation != null)
            {
                AddLoggingEventProperty("class-name", locationInformation.ClassName, properties);
                AddLoggingEventProperty("file-name", locationInformation.FileName, properties);
                AddLoggingEventProperty("method-name", locationInformation.MethodName, properties);
                AddLoggingEventProperty("line-number", locationInformation.LineNumber, properties);
            }
            
            if (loggingEvent.ExceptionObject != null)
            {
                AddLoggingEventProperty("exception-message", loggingEvent.ExceptionObject.Message, properties);
                
                if (loggingEvent.ExceptionObject.Data != null)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var o in loggingEvent.ExceptionObject.Data)
                    {
                        sb.Append(string.Format("{0}\r\n", o));
                    }
                    var data = sb.ToString();
                    if (!string.IsNullOrEmpty(data))
                    {
                        AddLoggingEventProperty("exception-data", data, properties);
                    }
                }
                if (loggingEvent.ExceptionObject.TargetSite != null)
                {
                    var site = string.Format("{0}.{1}", loggingEvent.ExceptionObject.TargetSite.ReflectedType, loggingEvent.ExceptionObject.TargetSite.Name);
                    
                    AddLoggingEventProperty("exception-targetsite", site, properties);
                }
                if (loggingEvent.ExceptionObject.Source != null) AddLoggingEventProperty("exception-source", loggingEvent.ExceptionObject.Source, properties);
                if (loggingEvent.ExceptionObject.StackTrace != null) AddLoggingEventProperty("exception-stacktrace", loggingEvent.ExceptionObject.StackTrace, properties);
                AddLoggingEventProperty("exception-hresult", loggingEvent.ExceptionObject.HResult.ToString(), properties);

                if (string.IsNullOrEmpty(loggingEvent.ExceptionObject.HelpLink))
                {

                    AddLoggingEventProperty("exception-help", string.Format("{0}{1}", "https://social.msdn.microsoft.com/Search/en-US?query=", loggingEvent.ExceptionObject.GetType().FullName), properties);
                }
                else
                {
                    AddLoggingEventProperty("exception-help", loggingEvent.ExceptionObject.HelpLink, properties);    
                }

                
            }
            AddLoggingEventProperty("user-name", loggingEvent.UserName, properties);
            AddLoggingEventProperty("domain", loggingEvent.Domain, properties);
            AddLoggingEventProperty("identity", loggingEvent.Identity, properties);
            if (loggingEvent.MessageObject != null)
            {
                AddLoggingEventProperty("message", loggingEvent.MessageObject.ToString(), properties);    
            }

        }
    }
}
