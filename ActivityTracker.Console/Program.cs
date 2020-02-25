using System;
using System.Runtime.Loader;
using System.Threading;
using ActivityTracker.Core.Features.ActivityTracking;
using ActivityTracker.Core.Features.Persistance;
using ActivityTracker.Core.Features.ProcessRunning;
using ActivityTracker.Core.Features.Screenshots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActivityTracker.Console
{
    class Program
    {
        private const int ACTIVITY_CHECK_INTERVAL_IN_SECONDS = 10;
        private const int SCREENSHOT_INTERVAL_IN_SECONDS = 45;
        private const int INACTIVITY_CHECK_INTERVAL_IN_SECONDS = 5;
        private const int INACTIVITY_THRESHOLD_IN_SECONDS = 30;

        private static IServiceProvider _services;
        private static ActivityLogEntry _currentLogEntry;

        private static Timer _activityTimer;
        // private static Timer _screenshotTimer;
        private static Timer _inactivityTimer;

        private static bool _userIsInactive;

        private static ILogger<Program> _log;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, ev) => HandleAppClosing();
            AssemblyLoadContext.Default.Unloading += ctx => HandleAppClosing();
            System.Console.CancelKeyPress += (s, ev) => HandleAppClosing();

            _services = CreateServices();

            _log = _services.GetService<ILogger<Program>>();

            _activityTimer = new Timer(LogActivity, null, TimeSpan.Zero, TimeSpan.FromSeconds(ACTIVITY_CHECK_INTERVAL_IN_SECONDS));
            // _screenshotTimer = new Timer(TakeScreenshot, null, TimeSpan.Zero, TimeSpan.FromSeconds(SCREENSHOT_INTERVAL_IN_SECONDS));
            _inactivityTimer = new Timer(CheckInactivity, null, TimeSpan.Zero, TimeSpan.FromSeconds(INACTIVITY_CHECK_INTERVAL_IN_SECONDS));

            Thread.Sleep(Timeout.Infinite);
        }

        private static void HandleAppClosing(){
            _activityTimer.Dispose();
            // _screenshotTimer.Dispose();
            _inactivityTimer.Dispose();

            if(_currentLogEntry == null)
                return;

            _log.LogInformation("App Closing. Saving last entry");
            var logsRepository = _services.GetService<IActivityService>();
            _ = logsRepository.EndActivityLogEntryAsync(_currentLogEntry).Result;
        }

        /// <summary>
        /// Configure the dependency injection services
        /// </summary>
        private static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();

            services.AddDbPersistance();

            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IActivityService, BashActivityService>();
            services.AddSingleton<IScreenshotService, BashScreenshotService>();
            services.AddSingleton<IDbPersistanceService>((serviceProvider) => {
                var service = new SqliteDbPersistanceService();
                service.RunMigrations(serviceProvider);
                return service;
            });

            services.AddLogging(opt => 
            { 
                opt.AddConsole(c =>
                {
                    c.TimestampFormat = "[HH:mm:ss] ";
                });
            });

            return services.BuildServiceProvider(false);
        }

        static void LogActivity(object state){
            var activityService = _services.GetService<IActivityService>();
            var screenshotService = _services.GetService<IScreenshotService>();

            var activity = activityService.GetCurrentWindowActivityAsync().Result;

            if(activity == null)
                return;

            if(_currentLogEntry != null 
                && _currentLogEntry.ApplicationTitle.Equals(activity.ApplicationTitle)
                && _currentLogEntry.WindowTitle.Equals(activity.WindowTitle))
                    return;

            if(_currentLogEntry != null)
                _ = activityService.EndActivityLogEntryAsync(_currentLogEntry).Result;

            _currentLogEntry = activityService.StartActivityLogEntryAsync(activity).Result;
            
            _log.LogInformation($"{_currentLogEntry.Id} {_currentLogEntry.ApplicationTitle} : {_currentLogEntry.WindowTitle}");
        }

        static void TakeScreenshot(object state){
            if(_currentLogEntry == null)
                return;

            var screenshotService = _services.GetService<IScreenshotService>();

            var screenshot = screenshotService.TakeScreenshotAsync().Result;
            _ = screenshotService.SaveScreenshotWithLogEntry(screenshot, _currentLogEntry);

            _log.LogInformation($"Screenshot Taken");
        }

        static void CheckInactivity(object state){
            var activityService = _services.GetService<IActivityService>();

            var inactivity = activityService.GetCurrentIdleTimeAsync().Result;
            if(!inactivity.HasValue)
                return;

            if(inactivity.Value.TotalSeconds > INACTIVITY_THRESHOLD_IN_SECONDS && !_userIsInactive)
            {
                _log.LogInformation("User has become inactive");
                _activityTimer.Change(Timeout.Infinite, Timeout.Infinite);
                // _screenshotTimer.Change(Timeout.Infinite, Timeout.Infinite);
                if(_currentLogEntry != null){
                    _ = activityService.EndActivityLogEntryAsync(_currentLogEntry).Result;
                    _currentLogEntry = null;
                }

                _userIsInactive = true;
            }
            else if(inactivity.Value.TotalSeconds <= INACTIVITY_THRESHOLD_IN_SECONDS && _userIsInactive){
                _activityTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(ACTIVITY_CHECK_INTERVAL_IN_SECONDS));
                // _screenshotTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(SCREENSHOT_INTERVAL_IN_SECONDS));
                _log.LogInformation("User has become active");
                _userIsInactive = false;
            }
        }
    }
}
