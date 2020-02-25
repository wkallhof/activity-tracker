using System;

namespace ActivityTracker.Core.Features.ActivityTracking
{
    public class ActivityLogSearchRequest
    {
        public string SearchText {get;set;}
        public DateTime? StartDateTime {get;set;}
        public DateTime? EndDateTime {get;set;}
    }
}