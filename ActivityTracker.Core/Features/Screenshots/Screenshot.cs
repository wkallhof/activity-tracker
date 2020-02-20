using System;

namespace ActivityTracker.Core.Features.Screenshots
{
    public class Screenshot
    {
        public int Id {get;set;}
        public int ActivityLogEntryId {get;set;}
        public DateTime CreateDate {get;set;}
        public byte[] Data {get;set;}
    }
}