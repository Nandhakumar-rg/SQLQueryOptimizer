using Microsoft.Extensions.DependencyInjection;
using SQLQueryOptimizer.Core.Interfaces;
using SQLQueryOptimizer.Core.Services;

namespace SQLQueryOptimizer.Core
{
    /// <summary>
    /// Extension methods for setting up SQL Query Optimizer services in an IServiceCollection
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds SQL Query Optimizer core services to the specified IServiceCollection
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to</param>
        /// <returns>The same service collection to enable method chaining</returns>
        public static IServiceCollection AddSqlQueryOptimizer(this IServiceCollection services)
        {
            // Register core services
            services.AddScoped<IQueryPerformanceCollector, SqlQueryPerformanceCollector>();
            services.AddScoped<IIndexAnalyzer, SqlIndexAnalyzer>();
            services.AddScoped<IQueryAnalyzer, SqlQueryAnalyzer>();

            return services;
        }

        /// <summary>
        /// Adds SQL Query Optimizer services with custom configuration to the specified IServiceCollection
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to</param>
        /// <param name="setupAction">An action to configure the SQL Query Optimizer options</param>
        /// <returns>The same service collection to enable method chaining</returns>
        public static IServiceCollection AddSqlQueryOptimizer(
            this IServiceCollection services,
            System.Action<SqlQueryOptimizerOptions> setupAction)
        {
            // Add default services
            services.AddSqlQueryOptimizer();

            // Configure options
            services.Configure(setupAction);

            return services;
        }
    }

    /// <summary>
    /// Options for configuring SQL Query Optimizer
    /// </summary>
    public class SqlQueryOptimizerOptions
    {
        /// <summary>
        /// Default connection string to use if one is not specified
        /// </summary>
        public string DefaultConnectionString { get; set; }

        /// <summary>
        /// Maximum query execution time in milliseconds
        /// </summary>
        public int MaxQueryExecutionTimeMs { get; set; } = 30000;

        /// <summary>
        /// Whether to log query analysis details
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Whether to analyze execution plans by default
        /// </summary>
        public bool AnalyzeExecutionPlans { get; set; } = true;

        /// <summary>
        /// Whether to analyze indexes by default
        /// </summary>
        public bool AnalyzeIndexes { get; set; } = true;
    }
}