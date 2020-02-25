using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActivityTracker.Core.Features.Persistance;
using ActivityTracker.Core.Features.ProcessRunning;
using ActivityTracker.Core.Features.Screenshots;

namespace ActivityTracker.Core.Features.ActivityTracking
{
    public interface IActivityService
    {
         Task<Activity> GetCurrentWindowActivityAsync();
         Task<TimeSpan?> GetCurrentIdleTimeAsync();
         Task<ActivityLogEntry> StartActivityLogEntryAsync(Activity activity);
         Task<ActivityLogEntry> EndActivityLogEntryAsync(ActivityLogEntry entry);
         Task<ActivityLogEntry> UpdateActivityLogAsync(ActivityLogEntry entry);
         Task<IEnumerable<ActivityLogEntry>> GetAllActivityLogEntriesAsync();
         Task<ActivityLogSearchResponse> SearchActivityLogEntriesAsync(ActivityLogSearchRequest request);
         Task<int> CountLoggedActivitiesAsync();
    }

    public class BashActivityService : IActivityService
    {
        private readonly IProcessRunner _processRunner;
        private readonly IDbPersistanceService _persistanceService;

        public BashActivityService(IProcessRunner processRunner, IDbPersistanceService persistanceService){
            _processRunner = processRunner;
            _persistanceService = persistanceService;
        }
        
        public async Task<Activity> GetCurrentWindowActivityAsync()
        {
            var outValue = await _processRunner.RunBashScriptProcessAsync("osascript ./mac.scpt");
            if(string.IsNullOrWhiteSpace(outValue))
                return null;

            var splitValue = outValue.Split(',');
            return new Activity(){
                ApplicationTitle = splitValue[0].Trim(),
                WindowTitle = splitValue[1].Trim()
            };
        }

        public async Task<TimeSpan?> GetCurrentIdleTimeAsync()
        {
            var outValue = await _processRunner.RunBashScriptProcessAsync("ioreg -c IOHIDSystem | awk '/HIDIdleTime/ {print $NF/1000000000; exit}'");
            if(string.IsNullOrWhiteSpace(outValue))
                return null;

            if(!double.TryParse(outValue, out var value))
                return null;

            return TimeSpan.FromSeconds(value);
        }

        public async Task<ActivityLogEntry> StartActivityLogEntryAsync(Activity activity){

            var logEntry = new ActivityLogEntry(){
                ApplicationTitle = activity.ApplicationTitle,
                WindowTitle = activity.WindowTitle,
                StartDateTime = DateTime.UtcNow
            };

            var query = $@"
                INSERT INTO {Tables.ActivityLogEntries}
                ({nameof(ActivityLogEntry.ApplicationTitle)}, 
                {nameof(ActivityLogEntry.WindowTitle)},
                {nameof(ActivityLogEntry.StartDateTime)})

                VALUES(@{nameof(ActivityLogEntry.ApplicationTitle)},
                @{nameof(ActivityLogEntry.WindowTitle)},
                @{nameof(ActivityLogEntry.StartDateTime)});
            
                SELECT * from {Tables.ActivityLogEntries} where Id=LAST_INSERT_ROWID()";

            return await _persistanceService.QuerySingleAsync<ActivityLogEntry>(query, logEntry);
        }

        public async Task<ActivityLogEntry> EndActivityLogEntryAsync(ActivityLogEntry entry){
            entry.EndDateTime = DateTime.UtcNow;
            return await UpdateActivityLogAsync(entry);
        }

        public async Task<ActivityLogEntry> UpdateActivityLogAsync(ActivityLogEntry entry){
            var query = $@"UPDATE {Tables.ActivityLogEntries}
                        SET {nameof(ActivityLogEntry.ApplicationTitle)} = @{nameof(ActivityLogEntry.ApplicationTitle)},
                            {nameof(ActivityLogEntry.WindowTitle)} = @{nameof(ActivityLogEntry.WindowTitle)},
                            {nameof(ActivityLogEntry.StartDateTime)} = @{nameof(ActivityLogEntry.StartDateTime)},
                            {nameof(ActivityLogEntry.EndDateTime)} = @{nameof(ActivityLogEntry.EndDateTime)}
                        WHERE
                            Id = @Id";

            await _persistanceService.ExecuteAsync(query, entry);
            return entry;
        }

        public async Task<int> CountLoggedActivitiesAsync(){
            var query = $@"SELECT COUNT(*) from {Tables.ActivityLogEntries}";
            return await _persistanceService.QuerySingleAsync<int>(query);
        }

        public async Task<IEnumerable<ActivityLogEntry>> GetAllActivityLogEntriesAsync()
        {
            var query = $@"SELECT * FROM {Tables.ActivityLogEntries}
                LEFT JOIN {Tables.Screenshots}
                on {Tables.Screenshots}.{nameof(Screenshot.ActivityLogEntryId)} = {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.Id)};";

            var logDictionary = new Dictionary<int, ActivityLogEntry>();

            var results = await _persistanceService.QueryAsync<ActivityLogEntry,Screenshot>(query, (entry, screenshot) => {
                	ActivityLogEntry logEntry;
                
                	if (!logDictionary.TryGetValue(entry.Id.Value, out logEntry))
                	{
                    	logEntry = entry;
                    	logEntry.Screenshots = new List<Screenshot>();
                    	logDictionary.Add(logEntry.Id.Value, logEntry);
                	}

                	logEntry.Screenshots.Add(screenshot);
                	return logEntry;
            }, null);

            results = results.Distinct();

            return results;
        }

        /*
            var query = $@"SELECT * FROM {Tables.ActivityLogEntries}
                WHERE
                    ({nameof(ActivityLogEntry.ApplicationTitle)} LIKE '%@{nameof(request.SearchText)}%' OR {nameof(ActivityLogEntry.WindowTitle)} LIKE '%@{nameof(request.SearchText)}%')
                    AND ('@{nameof(request.StartDateTime)}' is null OR {nameof(ActivityLogEntry.StartDateTime)} >= '@{nameof(request.StartDateTime)}')
                    ('@{nameof(request.EndDateTime)}' is null OR {nameof(ActivityLogEntry.StartDateTime)} <= '@{nameof(request.EndDateTime)}')

                ORDER BY {nameof(ActivityLogEntry.StartDateTime)};";
                */

        public async Task<ActivityLogSearchResponse> SearchActivityLogEntriesAsync(ActivityLogSearchRequest request)
        {
            var searchParams = new {
                SearchText = $"%{request.SearchText}%",
                StartDateTime = request.StartDateTime?.ToUniversalTime(),
                EndDateTime = request.EndDateTime?.ToUniversalTime(),
            };

            var query = $@"SELECT * FROM {Tables.ActivityLogEntries}
                WHERE 
                    ({nameof(ActivityLogEntry.ApplicationTitle)} LIKE @{nameof(searchParams.SearchText)} OR {nameof(ActivityLogEntry.WindowTitle)} LIKE @{nameof(searchParams.SearchText)})
                    AND (@{nameof(searchParams.StartDateTime)} is null OR {nameof(ActivityLogEntry.StartDateTime)} >= @{nameof(searchParams.StartDateTime)})
                    AND (@{nameof(searchParams.EndDateTime)} is null OR {nameof(ActivityLogEntry.StartDateTime)} <= @{nameof(searchParams.EndDateTime)})
                ORDER BY {nameof(ActivityLogEntry.StartDateTime)};";
                
            var results = await _persistanceService.QueryAsync<ActivityLogEntry>(query,searchParams);

            return new ActivityLogSearchResponse(){
                Results = results.ToList()
            };
        }
    }
}