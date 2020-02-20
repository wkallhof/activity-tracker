using System;
using System.IO;
using System.Threading.Tasks;
using ActivityTracker.Core.Features.ActivityTracking;
using ActivityTracker.Core.Features.Persistance;
using ActivityTracker.Core.Features.ProcessRunning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace ActivityTracker.Core.Features.Screenshots
{
    public interface IScreenshotService
    {
         Task<Screenshot> TakeScreenshotAsync();
         Task<Screenshot> SaveScreenshotWithLogEntry(Screenshot screenshot, ActivityLogEntry logEntry);
    }

    public class BashScreenshotService : IScreenshotService
    {
        private readonly IProcessRunner _processRunner;
        private readonly IDbPersistanceService _persistanceService;

        public BashScreenshotService(IProcessRunner processRunner, IDbPersistanceService persistanceService){
            _processRunner = processRunner;
            _persistanceService = persistanceService;
        }

        public async Task<Screenshot> TakeScreenshotAsync()
        {
            var script = "screencapture -t jpg -x $TMPDIR/screen.jpg ; echo $TMPDIR/screen.jpg";
            var path = await _processRunner.RunBashScriptProcessAsync(script);
            path = path.Remove(path.Length-1, 1);

            using var memoryStream = new MemoryStream();
            using var image = Image.Load(path, out var format);

            image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2)); 
            image.Save(memoryStream, format);

            return new Screenshot(){
                CreateDate = DateTime.UtcNow,
                Data = memoryStream.ToArray()
            };
        }

        public async Task<Screenshot> SaveScreenshotWithLogEntry(Screenshot screenshot, ActivityLogEntry logEntry){

            screenshot.ActivityLogEntryId = logEntry.Id.Value;

            var query = $@"
                INSERT INTO {Tables.Screenshots}
                    ({nameof(Screenshot.ActivityLogEntryId)},
                    {nameof(Screenshot.CreateDate)},
                    {nameof(Screenshot.Data)})

                VALUES(@{nameof(Screenshot.ActivityLogEntryId)}, 
                    @{nameof(Screenshot.CreateDate)},
                    @{nameof(Screenshot.Data)});
            
                SELECT * from {Tables.Screenshots} where Id=LAST_INSERT_ROWID()";

            return await _persistanceService.QuerySingleAsync<Screenshot>(query, screenshot);
        }
    }
}