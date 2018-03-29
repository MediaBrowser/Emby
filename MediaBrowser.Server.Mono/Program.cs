using MediaBrowser.Model.Logging;
using MediaBrowser.Server.Mono.Native;
using MediaBrowser.Server.Startup.Common;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Emby.Drawing;
using Emby.Server.Implementations;
using Emby.Server.Implementations.EnvironmentInfo;
using Emby.Server.Implementations.IO;
using Emby.Server.Implementations.Logging;
using Emby.Server.Implementations.Networking;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.System;
using ILogger = MediaBrowser.Model.Logging.ILogger;
using X509Certificate = System.Security.Cryptography.X509Certificates.X509Certificate;

namespace MediaBrowser.Server.Mono
{
    public class MainClass
    {
        private static ILogger _logger;
        private static IFileSystem FileSystem;
        private static IServerApplicationPaths _appPaths;
        private static ILogManager _logManager;

        private static readonly TaskCompletionSource<bool> ApplicationTaskCompletionSource = new TaskCompletionSource<bool>();
        private static bool _restartOnShutdown;

        public static void Main(string[] args)
        {
            var applicationPath = Assembly.GetEntryAssembly().Location;

            SetSqliteProvider();

            var options = new StartupOptions(Environment.GetCommandLineArgs());

            // Allow this to be specified on the command line.
            var customProgramDataPath = options.GetOption("-programdata");

            var appPaths = CreateApplicationPaths(applicationPath, customProgramDataPath);
            _appPaths = appPaths;

            using (var logManager = new SimpleLogManager(appPaths.LogDirectoryPath, "server"))
            {
                _logManager = logManager;

                var task = logManager.ReloadLogger(LogSeverity.Debug, CancellationToken.None);
                Task.WaitAll(task);
                logManager.AddConsoleOutput();

                var logger = _logger = logManager.GetLogger("Main");

                ApplicationHost.LogEnvironmentInfo(logger, appPaths, true);

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                RunApplication(appPaths, logManager, options);

                _logger.Info("Disposing app host");

                if (_restartOnShutdown)
                {
                    StartNewInstance(options);
                }
            }
        }

        private static void SetSqliteProvider()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        }

        private static ServerApplicationPaths CreateApplicationPaths(string applicationPath, string programDataPath)
        {
            if (string.IsNullOrEmpty(programDataPath))
            {
                programDataPath = ApplicationPathHelper.GetProgramDataPath(applicationPath);
            }

            var appFolderPath = Path.GetDirectoryName(applicationPath);

            return new ServerApplicationPaths(programDataPath, appFolderPath, Path.GetDirectoryName(applicationPath));
        }

        private static void RunApplication(ServerApplicationPaths appPaths, ILogManager logManager, StartupOptions options)
        {
            // Allow all https requests
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });

            var environmentInfo = GetEnvironmentInfo(options);

            var fileSystem = new ManagedFileSystem(logManager.GetLogger("FileSystem"), environmentInfo, null, appPaths.TempDirectory);

            FileSystem = fileSystem;

            using (var appHost = new MonoAppHost(appPaths,
                logManager,
                options,
                fileSystem,
                new PowerManagement(),
                "emby.mono.zip",
                environmentInfo,
                new NullImageEncoder(),
                new SystemEvents(logManager.GetLogger("SystemEvents")),
                new NetworkManager(logManager.GetLogger("NetworkManager"), environmentInfo)))
            {
                if (options.ContainsOption("-v"))
                {
                    Console.WriteLine(appHost.ApplicationVersion.ToString());
                    return;
                }

                Console.WriteLine("appHost.Init");

                appHost.Init();

                appHost.ImageProcessor.ImageEncoder = ImageEncoderHelper.GetImageEncoder(_logger, logManager, fileSystem, options, () => appHost.HttpClient, appPaths, environmentInfo, appHost.LocalizationManager);

                Console.WriteLine("Running startup tasks");

                var task = appHost.RunStartupTasks();
                Task.WaitAll(task);

                task = ApplicationTaskCompletionSource.Task;

                Task.WaitAll(task);
            }
        }

        private static EnvironmentInfo GetEnvironmentInfo(StartupOptions options)
        {
            var operatingSystem = Model.System.OperatingSystem.Linux;

            if (string.Equals(options.GetOption("-os"), "freebsd", StringComparison.OrdinalIgnoreCase))
            {
                operatingSystem = Model.System.OperatingSystem.BSD;
            }

            return new EnvironmentInfo()
            {
                OperatingSystem = operatingSystem,
                SystemArchitecture = Environment.Is64BitOperatingSystem ? Architecture.X64 : Architecture.Arm
            };
        }

        /// <summary>
        /// Handles the UnhandledException event of the CurrentDomain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="UnhandledExceptionEventArgs"/> instance containing the event data.</param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;

            new UnhandledExceptionWriter(_appPaths, _logger, _logManager, FileSystem, new ConsoleLogger()).Log(exception);

            if (!Debugger.IsAttached)
            {
                var message = LogHelper.GetLogMessage(exception).ToString();

                if (message.IndexOf("InotifyWatcher", StringComparison.OrdinalIgnoreCase) == -1 &&
                    message.IndexOf("_IOCompletionCallback", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    Environment.Exit(System.Runtime.InteropServices.Marshal.GetHRForException(exception));
                }
            }
        }

        public static void Shutdown()
        {
            ApplicationTaskCompletionSource.SetResult(true);
        }

        public static void Restart()
        {
            _restartOnShutdown = true;

            Shutdown();
        }

        private static void StartNewInstance(StartupOptions startupOptions)
        {
            _logger.Info("Starting new instance");

            string module = startupOptions.GetOption("-restartpath");
            string commandLineArgsString = startupOptions.GetOption("-restartargs") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(module))
            {
                module = Environment.GetCommandLineArgs().First();
            }
            if (!startupOptions.ContainsOption("-restartargs"))
            {
                var args = Environment.GetCommandLineArgs()
                    .Skip(1)
                    .Select(NormalizeCommandLineArgument)
                    .ToArray();

                commandLineArgsString = string.Join(" ", args);
            }

            _logger.Info("Executable: {0}", module);
            _logger.Info("Arguments: {0}", commandLineArgsString);

            Process.Start(module, commandLineArgsString);
        }

        private static string NormalizeCommandLineArgument(string arg)
        {
            if (arg.IndexOf(" ", StringComparison.OrdinalIgnoreCase) == -1)
            {
                return arg;
            }

            return "\"" + arg + "\"";
        }
    }

    class NoCheckCertificatePolicy : ICertificatePolicy
    {
        public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem)
        {
            return true;
        }
    }
}
