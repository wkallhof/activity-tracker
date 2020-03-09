using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActivityTracker.Core.Features.Persistance;

namespace ActivityTracker.Core.Features.Categorizing
{
    public interface ICategorizingService
    {
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<Category> CreateCategoryAsync(string title);
        Task DeleteCategoryAsync(int? categoryId);

        Task CategorizeActivityLogEntry(int? entry, int? categoryId);
    }

    public class CategorizingService : ICategorizingService
    {
        private readonly IDbPersistanceService _persistanceService;

        public CategorizingService(IDbPersistanceService persistanceService){
            _persistanceService = persistanceService;
        }

        public async Task CategorizeActivityLogEntry(int? entryId, int? categoryId)
        {
            if(!entryId.HasValue)
                throw new ArgumentNullException($"Property {nameof(entryId)} is required.");

            if(!categoryId.HasValue)
                throw new ArgumentNullException($"Property {nameof(categoryId)} is required.");

            var existingMapping = await FindActivityLogCategoryMapping(entryId.Value, categoryId.Value);
            if(existingMapping != null)
                return;

            var mapping = new ActivityLogEntryCategoryMapping(){
                CategoryId = categoryId.Value,
                ActivityLogEntryId = entryId.Value
            };

            var query = $@"
                INSERT INTO {Tables.ActivityLogEntryCategoryMapping}
                ({nameof(ActivityLogEntryCategoryMapping.CategoryId)}, 
                {nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)})

                VALUES(@{nameof(ActivityLogEntryCategoryMapping.CategoryId)},
                @{nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)});";

            await _persistanceService.ExecuteAsync(query, mapping);
        }

        public async Task<Category> CreateCategoryAsync(string title)
        {
            if(string.IsNullOrWhiteSpace(title))
                throw new ArgumentNullException($"Property {nameof(title)} is required.");

            //ensure that this category doesn't already exist
            var allCategories = await GetAllCategoriesAsync();
            if(allCategories.Any(x => x.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
                throw new DuplicateCategoryException($"The category with the title: {title} already exists.");

            var category = new Category(){
                Title = title,
                CreateDate = DateTime.UtcNow
            };

            var query = $@"
                INSERT INTO {Tables.Categories}
                ({nameof(Category.Title)}, 
                {nameof(Category.CreateDate)})

                VALUES(@{nameof(Category.Title)},
                @{nameof(Category.CreateDate)});
            
                SELECT * from {Tables.Categories} where Id=LAST_INSERT_ROWID()";

            return await _persistanceService.QuerySingleAsync<Category>(query, category);
        }

        public async Task DeleteCategoryAsync(int? categoryId)
        {
            if(!categoryId.HasValue)
                throw new ArgumentNullException($"Property {nameof(categoryId)} is required.");

            var query = $@"
                DELETE FROM {Tables.Categories}
                WHERE {nameof(Category.Id)} = @Id;
                
                DELETE FROM {Tables.ActivityLogEntryCategoryMapping}
                WHERE {nameof(ActivityLogEntryCategoryMapping.CategoryId)} = @Id;";
            
            await _persistanceService.ExecuteAsync(query, new { Id = categoryId });
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            var query = $@"SELECT * From {Tables.Categories};";
            return await _persistanceService.QueryAsync<Category>(query);
        }

        private async Task<ActivityLogEntryCategoryMapping> FindActivityLogCategoryMapping(int entryId, int categoryId){
            var findExistingCategoryMappingQuery = $@"
                SELECT * FROM {Tables.ActivityLogEntryCategoryMapping}
                WHERE {nameof(ActivityLogEntryCategoryMapping.ActivityLogEntryId)} = @{nameof(entryId)}
                AND {nameof(ActivityLogEntryCategoryMapping.CategoryId)} = @{nameof(categoryId)}";

            return await _persistanceService.QuerySingleOrDefaultAsync<ActivityLogEntryCategoryMapping>(findExistingCategoryMappingQuery, new { entryId, categoryId });
        }
    }
}