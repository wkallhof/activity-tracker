using ActivityTracker.Core.Features.ActivityTracking;
using ActivityTracker.Core.Features.Persistance;
using FluentMigrator;

namespace ActivityTracker.Core.Migrations
{
    [Migration(202002142216)]
    public class CreateActivityLogEntriesTable : Migration
    {
        public override void Up()
        {
            Create.Table(Tables.ActivityLogEntries)
                .WithColumn(nameof(ActivityLogEntry.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(ActivityLogEntry.ApplicationTitle)).AsString().NotNullable()
                .WithColumn(nameof(ActivityLogEntry.WindowTitle)).AsString().Nullable()
                .WithColumn(nameof(ActivityLogEntry.StartDateTime)).AsDateTime2().NotNullable()
                .WithColumn(nameof(ActivityLogEntry.EndDateTime)).AsDateTime2().Nullable();
        }

        public override void Down()
        {
            Delete.Table(Tables.ActivityLogEntries);
        }
    }
}