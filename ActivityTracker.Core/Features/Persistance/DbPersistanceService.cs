using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using ActivityTracker.Core.Migrations;
using Dapper;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using static System.Environment;

namespace ActivityTracker.Core.Features.Persistance
{
    public interface IDbPersistanceService
    {
         void RunMigrations(IServiceProvider serviceProvider);
        Task<T> QuerySingleAsync<T>(string query, object parameters = null);
        Task<IEnumerable<T>> QueryAsync<T>(string query, object parameters = null);
        Task<IEnumerable<T1>> QueryAsync<T1, T2>(string query, Func<T1,T2,T1> map, object parameters = null);
        Task ExecuteAsync(string query, object parameters = null);
    }

    public class SqliteDbPersistanceService : IDbPersistanceService
    {
        private string _connectionString = $"Data Source={Path.Join(GetFolderPath(SpecialFolder.ApplicationData), "/ActivityTracker.db")}";

        public void RunMigrations(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }

        public async Task<T> QuerySingleAsync<T>(string query, object parameters = null){
            using var db = new SqliteConnection(_connectionString);
            return await db.QuerySingleAsync<T>(query, parameters);
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string query, object parameters = null){
            using var db = new SqliteConnection(_connectionString);
            return await db.QueryAsync<T>(query, parameters);
        }

        public async Task<IEnumerable<T1>> QueryAsync<T1, T2>(string query, Func<T1,T2,T1> map, object parameters = null){
            using var db = new SqliteConnection(_connectionString);
            return await db.QueryAsync<T1, T2, T1>(query, map, parameters);
        }

        public async Task ExecuteAsync(string query, object parameters = null){
            using var db = new SqliteConnection(_connectionString);
            await db.ExecuteAsync(query, parameters);
        }
    }

    public static class DbPersistanceServiceProviderExtensions{
        public static IServiceCollection AddDbPersistance(this IServiceCollection services){
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddSQLite()
                    .WithGlobalConnectionString($"Data Source={Path.Join(GetFolderPath(SpecialFolder.ApplicationData), "/ActivityTracker.db")}")
                    .ScanIn(typeof(CreateActivityLogEntriesTable).Assembly).For.Migrations());

            services.AddLogging(lb => lb.AddFluentMigratorConsole());

            return services;
        }
    }
}