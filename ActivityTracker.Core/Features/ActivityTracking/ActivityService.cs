using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActivityTracker.Core.Features.Categorizing;
using ActivityTracker.Core.Features.Persistance;
using ActivityTracker.Core.Features.ProcessRunning;

namespace ActivityTracker.Core.Features.ActivityTracking
{
    public interface IActivityService
    {
         Task<Activity> GetCurrentWindowActivityAsync();
         Task<TimeSpan?> GetCurrentIdleTimeAsync();
         Task<ActivityLogEntry> StartActivityLogEntryAsync(Activity activity);
         Task<ActivityLogEntry> EndActivityLogEntryAsync(ActivityLogEntry entry);
         Task<ActivityLogEntry> UpdateActivityLogAsync(ActivityLogEntry entry);
         Task DeleteActivityLogEntriesAsync(List<int> ids);
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

        public async Task<ActivityLogSearchResponse> SearchActivityLogEntriesAsync(ActivityLogSearchRequest request)
        {
            var searchParams = new {
                SearchText = $"%{request.SearchText}%",
                StartDateTime = request.StartDateTime?.ToUniversalTime(),
                EndDateTime = request.EndDateTime?.ToUniversalTime(),
            };

            var query = $@"SELECT {Tables.ActivityLogEntries}.*, {Tables.Categories}.*
                FROM {Tables.ActivityLogEntries}
                    LEFT JOIN {Tables.ActivityLogEntryCategoryMapping} ON {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.Id)} = {Tables.ActivityLogEntryCategoryMapping}.{nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)}
                    LEFT JOIN {Tables.Categories} ON {Tables.Categories}.{nameof(Category.Id)} = {Tables.ActivityLogEntryCategoryMapping}.{nameof(ActivityLogEntryCategoryMapping.CategoryId)}
                WHERE 
                    ({Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.ApplicationTitle)} LIKE @{nameof(searchParams.SearchText)} OR {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.WindowTitle)} LIKE @{nameof(searchParams.SearchText)})
                    AND (@{nameof(searchParams.StartDateTime)} is null OR {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.StartDateTime)} >= @{nameof(searchParams.StartDateTime)})
                    AND (@{nameof(searchParams.EndDateTime)} is null OR {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.StartDateTime)} <= @{nameof(searchParams.EndDateTime)})
                ORDER BY {Tables.ActivityLogEntries}.{nameof(ActivityLogEntry.StartDateTime)};";
            

            var mapDictionary = new Dictionary<int, ActivityLogEntry>();
            var results = await _persistanceService.QueryAsync<ActivityLogEntry, Category>(query,
            (entry, category) => {
                if(!mapDictionary.TryGetValue(entry.Id.Value, out var logEntry)){
                    logEntry = entry;
                    mapDictionary.Add(entry.Id.Value, logEntry);
                }

                if(category != null && category.Title != null)
                    logEntry.Categories.Add(category.Title);
                    
                return logEntry;
            }, searchParams);

            return new ActivityLogSearchResponse(){
                Results = results.Distinct().ToList()
            };
        }

        public async Task DeleteActivityLogEntriesAsync(List<int> ids){
            var query = $@"DELETE FROM {Tables.ActivityLogEntries}
                           WHERE {nameof(ActivityLogEntry.Id)} IN @Ids;
                           
                           DELETE FROM {Tables.ActivityLogEntryCategoryMapping}
                           WHERE {nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)} IN @Ids";
            
            await _persistanceService.ExecuteAsync(query, new { Ids = ids });
        }
    }
}