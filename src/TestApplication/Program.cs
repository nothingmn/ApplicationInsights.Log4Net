using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApplicationInsights.Log4Net;
using log4net;
using log4net.Config;
using log4net.Core;

namespace TestApplication
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));

            Task.Factory.StartNew(() =>
            {
                
                var logger = log4net.LogManager.GetLogger(typeof (Program));
                log4net.ThreadContext.Properties["user-agent"] = "some cool user agent goes here";
                log4net.ThreadContext.Properties["user-id"] = "USERIDGOESHERE";
                log4net.ThreadContext.Properties["session-id"] = "SSESSIONIDGOESHERE";

                var exc = new DllNotFoundException("this is some random exception!");
                for (int x = 4000; x <= 5000; x++)
                {
                    Console.WriteLine("Logging");
                    logger.Info("Info with exception" + x, exc);
                    logger.Debug("Debug with exception" + x, exc);
                    logger.Error("Error with exception" + x, exc);
                    logger.Fatal("Fatal with exception" + x, exc);
                    logger.Warn("Warn with exception" + x, exc);

                    logger.Info("Info" + x);
                    logger.Debug("Debug" + x);
                    logger.Error("Error" + x);
                    logger.Fatal("Fatal" + x);
                    logger.Warn("Warn" + x);

                    try
                    {
                        throw exc;
                    }
                    catch (Exception e)
                    {
                        logger.Error("there was an error!" + x, e);
                    }


                    Console.WriteLine("Logged");
                    Task.Delay(1000*2).Wait();
                }

            });

            Console.WriteLine("Sleeping");
            while (true)
            {
                Task.Delay(1000).Wait();
            }
        }

    }
}