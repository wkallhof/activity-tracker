using System;
using System.Collections.Generic;
using ActivityTracker.Core.Features.Screenshots;

namespace ActivityTracker.Core.Features.ActivityTracking
{
    public class ActivityLogEntry
    {
        public int? Id {get;set;}
        public string ApplicationTitle {get;set;}
        public string WindowTitle {get;set;}
        public DateTime StartDateTime {get;set;}
        public DateTime? EndDateTime {get;set;}
        public List<string> Categories {get;set;}

        public List<Screenshot> Screenshots {get;set;}
    }
}