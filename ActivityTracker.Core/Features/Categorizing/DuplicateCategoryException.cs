using System;

namespace ActivityTracker.Core.Features.Categorizing
{
    public class DuplicateCategoryException : Exception
    {
        public DuplicateCategoryException() { }
        public DuplicateCategoryException(string message) : base(message) { }
        public DuplicateCategoryException(string message, System.Exception inner) : base(message, inner) { }
    }
}