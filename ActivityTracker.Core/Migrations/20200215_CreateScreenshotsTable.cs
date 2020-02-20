using ActivityTracker.Core.Features.ActivityTracking;
using ActivityTracker.Core.Features.Persistance;
using ActivityTracker.Core.Features.Screenshots;
using FluentMigrator;

namespace ActivityTracker.Core.Migrations
{
    [Migration(202002151200)]
    public class CreateScreenshotsTable : Migration
    {
        public override void Up()
        {
            Create.Table(Tables.Screenshots)
                .WithColumn(nameof(Screenshot.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(Screenshot.ActivityLogEntryId)).AsInt32().NotNullable()
                .WithColumn(nameof(Screenshot.CreateDate)).AsDateTime2().NotNullable()
                .WithColumn(nameof(Screenshot.Data)).AsBinary().NotNullable();
        }

        public override void Down()
        {
            Delete.Table(Tables.Screenshots);
        }
    }
}