using ActivityTracker.Core.Features.Categorizing;
using FluentMigrator;

namespace ActivityTracker.Core.Features.Persistance.Migrations
{
    [Migration(202003081720)]
    public class CreateCategories : Migration
    {
        public override void Up()
        {
            Create.Table(Tables.Categories)
                .WithColumn(nameof(Category.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(Category.Title)).AsString().NotNullable()
                .WithColumn(nameof(Category.CreateDate)).AsDateTime2().NotNullable();

            Create.Table(Tables.ActivityLogEntryCategoryMapping)
                .WithColumn(nameof(ActivityLogEntryCategoryMapping.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)).AsInt32().NotNullable()
                .WithColumn(nameof(ActivityLogEntryCategoryMapping.CategoryId)).AsInt32().NotNullable();
        }

        public override void Down()
        {
            Delete.Table(Tables.Categories);
            Delete.Table(Tables.ActivityLogEntryCategoryMapping);
        }
    }
}