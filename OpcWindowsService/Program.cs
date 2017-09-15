using System;
using System.Globalization;
using System.Linq;
using log4net;
using log4net.Config;
using Topshelf;
using Topshelf.Logging;
using Topshelf.Ninject;

namespace Opisense.OpcWindowsService
{
    public static class Program
    {
	    private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// The main entry point for the application.
        /// Command line usage:
        ///     Gives help on command line
        ///		    *.exe help
        ///     Install as service
        ///		    *.exe install (default name: Opisense.OpcService, start as LocalSystem)
        ///		    *.exe install -username:"My Name" -password="my_password" -servicename:my_service_name -instance:1
        ///     Start service
        ///		    *.exe start -servicename:my_service_name -instance:1
        ///     Stop service
        ///		    *.exe stop -servicename:my_service_name -instance:1
        ///     Uninstall service
        ///		    *.exe uninstall -servicename:my_service_name -instance:1
        ///     Run as a standard console
        ///		*.exe
        ///     Run as console, push a single variable and terminate
        ///         *.exe -pushvariable:var_id -pushvalue:var_value
        /// </summary>
        private static void Main(string[] args)
        {
            //Toshelf "forgets" to handle username and password, that's why we intercept them
            var userNameArgument = args.FirstOrDefault(a => a.StartsWith("-username:", true, CultureInfo.InvariantCulture));
            var userName = userNameArgument?.Substring("-username:".Length).Trim() ?? "";

            var passwordArgument = args.FirstOrDefault(a => a.StartsWith("-password:", true, CultureInfo.InvariantCulture));
            var password = passwordArgument?.Substring("-password:".Length).Trim();

            var pushVariableId = int.MinValue;
            var pushValue = double.MinValue;

            XmlConfigurator.Configure();
            Logger.Info("Opc windows service process starting");

			HostFactory.Run(configurator =>
            {
                configurator.AddCommandLineDefinition("pushvariable", arg => pushVariableId = int.Parse(arg));
                configurator.AddCommandLineDefinition("pushvalue", arg => pushValue = int.Parse(arg));
                configurator.ApplyCommandLine();

                HostLogger.Current.Get("Main").Debug("Run host factory");
                configurator.UseNinject(new ServiceNinjectModule()); 
                configurator.UseLog4Net();

				configurator.Service<Service>(s =>                        
                {
                    XmlConfigurator.Configure();
                    s.ConstructUsingNinject();
                    if (pushVariableId != int.MinValue && Math.Abs(pushValue - double.MinValue) > double.Epsilon)
                    {
                        HostLogger.Current.Get("Main").Warn("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        HostLogger.Current.Get("Main").Warn("!!! RUNNING IN PUSH VARIABLE MODE, NO OPC ACQUISITION !!!");
                        HostLogger.Current.Get("Main").Warn("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        s.WhenStarted(tc => tc.PushVariable(pushVariableId, pushValue));
                        s.WhenStopped(tc => {});
                    }
                    else
                    {
                        s.WhenStarted(tc => tc.Start());
                        s.WhenStopped(tc => tc.Stop());
                    }              
                });

                if (string.IsNullOrEmpty(userName) || password == null)
                {
                    configurator.RunAsLocalSystem();
                }
                else
                {
                    configurator.RunAs(userName, password);
                }

                configurator.SetDescription("Polls OPC groups and sends data to Opisense");        
                configurator.SetDisplayName("Opisense.OpcService");        
                configurator.SetServiceName("Opisense.OpcService");        

                configurator.StartAutomatically();
            });
        }
    }
}
