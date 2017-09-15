using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using log4net;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace Opisense.OpcWindowsService
{
    public class Service
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Service));

        private const string OpcConfigFileKey = "OpcConfigFile";
        private const string DefaultOpcServerKey = "DefaultOpcServer";
        private readonly TimeSpan workerStartTimeout = TimeSpan.FromSeconds(30);
        private readonly TimeSpan workerStopTimeout = TimeSpan.FromSeconds(30);

        private CancellationTokenSource stopOpcWorkerCancellationTokenSource;
        private readonly IServiceWorker opcServiceWorker;
        private FileSystemWatcher watcher;
        private bool serviceIsStarted;
        private byte[] currentConfigFileChecksum;


        private static readonly RetryPolicy<FileLockedStrategy> FileLockedRetryPolicy = new RetryPolicy<FileLockedStrategy>(5, TimeSpan.FromSeconds(1));

        private class FileLockedStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex)
            {
                return ex is UnauthorizedAccessException;
            }
        }

        public Service(IServiceWorker opcServiceWorker)
        {
            this.opcServiceWorker = opcServiceWorker;
        }

        private static byte[] GetFileChecksum(string fullName)
        {
            //When a file is created/modified, it is not always immediately available
            FileLockedRetryPolicy.ExecuteAction(() =>
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(fullName))
                {
                    return md5.ComputeHash(stream);
                }
            });

            return new byte[0];
        }

        private static bool ChecksumAreIdentical(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;

            return !left.Where((t, i) => t != right[i]).Any();
        }


        public void Start(bool askedByServiceManager = true)
        {
            if (serviceIsStarted)
                return;

            stopOpcWorkerCancellationTokenSource = new CancellationTokenSource();

			Logger.Info($"{nameof(Service)} - STARTING");

            var configFileName = ConfigurationManager.AppSettings.Get(OpcConfigFileKey);
            if (string.IsNullOrWhiteSpace(configFileName))
            {
                Logger.Error($"The OPC config file name is missing or empty in app.config (key:{OpcConfigFileKey})");
                return;
            }

            var defaultOpcServer = ConfigurationManager.AppSettings.Get(DefaultOpcServerKey) ?? "UNSPECIFIED DEFAULT OPC SERVER";

            Logger.Info($"Opc config file name: '{configFileName}'");
            Logger.Info($"Default OPC server: '{defaultOpcServer}'");

            var configFile = new FileInfo(configFileName);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(workerStartTimeout);
            try
            {
                //Allow workerStartTimeout to the worker to start
                opcServiceWorker.Run(configFile.FullName, defaultOpcServer, stopOpcWorkerCancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
                cancellationTokenSource.Dispose();
            }
            catch (OperationCanceledException)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    return;

                var message = $"The OPC worker did not start within {workerStartTimeout:g}, the service will stop";
                Logger.Error(message);
                throw new Exception(message);
            }

            currentConfigFileChecksum = GetFileChecksum(configFile.FullName);

            if (askedByServiceManager)
            {
                //The watcher will detect conffig file changes
                watcher = new FileSystemWatcher(configFile.DirectoryName)
                {
                    Filter = configFile.Name,
                    IncludeSubdirectories = false,
                };
                watcher.Changed += ConfigFileChanged;
                watcher.Deleted += ConfigFileDeleted;
                watcher.EnableRaisingEvents = true;
            }
            Logger.Info($"{nameof(Service)} - STARTED");
            serviceIsStarted = true;
		}

        public void Stop(bool askedByServiceManager = true)
        {
            if (!serviceIsStarted)
                return;
            serviceIsStarted = false;

            Logger.Info($"{nameof(Service)} - ENDING");

            stopOpcWorkerCancellationTokenSource.Cancel();
            try
            {
                //Allow workerStopTimeout to the worker to stop
                var cancellationTokenSource = new CancellationTokenSource(workerStopTimeout);
                opcServiceWorker.WaitForEnd(cancellationTokenSource.Token);
                cancellationTokenSource.Dispose();
            }
            catch
            {
                //Never die on a stop
            }

            try
            {
                if (askedByServiceManager)
                {
                    watcher.Changed -= ConfigFileChanged;
                    watcher.Created -= ConfigFileChanged;
                    watcher.Deleted -= ConfigFileDeleted;
                    watcher.Dispose();
                }

            }
            catch
            {
                //Never die on a stop
            }
            stopOpcWorkerCancellationTokenSource.Dispose();
            Logger.Info($"{nameof(Service)} - ENDED");
		}

        private void ConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            var checksumOfNewConfigFile = GetFileChecksum(e.FullPath);
            if (ChecksumAreIdentical(currentConfigFileChecksum, checksumOfNewConfigFile))
            {
                Logger.Info($"The config file '{e.FullPath}' was modified but it's content did not changed, keeping the existing running context");
                return;
            }

            Logger.Info($"The service detected a change in config file '{e.FullPath}' that will restart the OPC acquisition");
            Stop(false);
            Start(false);
            currentConfigFileChecksum = checksumOfNewConfigFile;
        }

        private void ConfigFileDeleted(object sender, FileSystemEventArgs e)
        {
            Logger.Info($"The service detected the deletion of config file '{e.FullPath}' that will STOP the OPC acquisition until the config file is restored");
            currentConfigFileChecksum = new byte[0];
            Stop(false);
        }

        public void PushVariable(int pushVariableId, double pushValue)
        {
            Logger.Info($"Pushing value {pushValue} to variable id {pushVariableId}");
            opcServiceWorker.PushVariable(pushVariableId, pushValue);
            Logger.Info($"!!! The service is now inactive");
        }
    }
}