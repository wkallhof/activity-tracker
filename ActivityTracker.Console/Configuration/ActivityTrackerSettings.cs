using System.Collections.Generic;

namespace ActivityTracker.Console.Configuration
{
    public class ActivityTrackerSettings
    {
        public List<string> ActivityRegexExclude {get;set;} = new List<string>();
    }
}