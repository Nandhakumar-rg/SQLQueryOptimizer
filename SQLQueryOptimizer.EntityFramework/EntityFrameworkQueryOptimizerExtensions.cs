using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SQLQueryOptimizer.Core.Interfaces;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.EntityFramework
{
    /// <summary>
    /// Extension methods for analyzing Entity Framework Core queries
    /// </summary>
    public static class EntityFrameworkQueryOptimizerExtensions
    {
        /// <summary>
        /// Analyzes an Entity Framework Core IQueryable for optimization opportunities
        /// </summary>
        /// <typeparam name="T">Type of entity being queried</typeparam>
        /// <param name="query">The IQueryable to analyze</param>
        /// <param name="queryAnalyzer">The query analyzer service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Analysis result with optimization suggestions</returns>
        public static async Task<QueryAnalysisResult> AnalyzeAsync<T>(
            this IQueryable<T> query,
            IQueryAnalyzer queryAnalyzer,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (queryAnalyzer == null)
                throw new ArgumentNullException(nameof(queryAnalyzer));

            // Get the DbContext from the query
            var dbContext = GetDbContextFromQuery(query);
            if (dbContext == null)
                throw new InvalidOperationException("Unable to get DbContext from the provided query");

            // Get the connection string
            var connectionString = dbContext.Database.GetDbConnection().ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Unable to get connection string from DbContext");

            // Get the SQL query
            var sqlQuery = query.ToQueryString();
            if (string.IsNullOrWhiteSpace(sqlQuery))
                throw new InvalidOperationException("Unable to generate SQL query from IQueryable");

            // Analyze the query
            return await queryAnalyzer.AnalyzeQueryAsync(sqlQuery, connectionString, cancellationToken);
        }

        /// <summary>
        /// Optimizes an Entity Framework Core IQueryable based on analysis
        /// </summary>
        /// <typeparam name="T">Type of entity being queried</typeparam>
        /// <param name="query">The IQueryable to optimize</param>
        /// <param name="queryAnalyzer">The query analyzer service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optimization suggestions for the query</returns>
        public static async Task<QueryAnalysisResult> OptimizeAsync<T>(
            this IQueryable<T> query,
            IQueryAnalyzer queryAnalyzer,
            CancellationToken cancellationToken = default)
        {
            var result = await query.AnalyzeAsync(queryAnalyzer, cancellationToken);

            // The result contains optimization suggestions which the user can apply
            // We cannot directly modify the IQueryable as EF Core builds the expression tree

            return result;
        }

        /// <summary>
        /// Gets the DbContext from an IQueryable
        /// </summary>
        private static DbContext GetDbContextFromQuery<T>(IQueryable<T> query)
        {
            if (query is null)
                return null;

            // Try to get the DbContext from the query provider
            if (query.Provider is Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryProvider entityQueryProvider)
            {
                var dbContextField = typeof(Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryProvider)
                    .GetField("_queryCompiler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (dbContextField != null)
                {
                    var queryCompiler = dbContextField.GetValue(entityQueryProvider);

                    var contextFactoryField = queryCompiler.GetType()
                        .GetField("_queryContextFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (contextFactoryField != null)
                    {
                        var queryContextFactory = contextFactoryField.GetValue(queryCompiler);

                        var dependenciesProperty = queryContextFactory.GetType()
                            .GetProperty("Dependencies", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                        if (dependenciesProperty != null)
                        {
                            var dependencies = dependenciesProperty.GetValue(queryContextFactory);

                            var currentContextProperty = dependencies.GetType()
                                .GetProperty("CurrentContext", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                            if (currentContextProperty != null)
                            {
                                var currentContext = currentContextProperty.GetValue(dependencies);

                                var contextProperty = currentContext.GetType()
                                    .GetProperty("Context", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (contextProperty != null)
                                {
                                    return contextProperty.GetValue(currentContext) as DbContext;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}