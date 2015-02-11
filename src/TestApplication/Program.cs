using System;
using System.Threading.Tasks;
using log4net.Config;

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
                log4net.ThreadContext.Properties["UserAgent"] = "some cool user agent goes here";
                log4net.ThreadContext.Properties["UserId"] = Guid.NewGuid().ToString();
                log4net.ThreadContext.Properties["SessionId"] = Guid.NewGuid().ToString();

                var exc = new DllNotFoundException("this is some random exception!");
                for (int x = 6000; x <= 7000; x++)
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