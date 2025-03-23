using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SQLQueryOptimizer.Core.Interfaces;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Dapper
{
    /// <summary>
    /// Extension methods for analyzing and optimizing Dapper queries
    /// </summary>
    public static class DapperQueryOptimizerExtensions
    {
        /// <summary>
        /// Analyzes a SQL query that would be executed with Dapper
        /// </summary>
        /// <param name="connection">The database connection</param>
        /// <param name="sql">The SQL query to analyze</param>
        /// <param name="queryAnalyzer">The query analyzer service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Analysis result with optimization suggestions</returns>
        public static async Task<QueryAnalysisResult> AnalyzeQueryAsync(
            this IDbConnection connection,
            string sql,
            IQueryAnalyzer queryAnalyzer,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            if (queryAnalyzer == null)
                throw new ArgumentNullException(nameof(queryAnalyzer));

            // Get the connection string
            string connectionString = GetConnectionString(connection);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Unable to get connection string from IDbConnection");

            // Analyze the query
            return await queryAnalyzer.AnalyzeQueryAsync(sql, connectionString, cancellationToken);
        }

        /// <summary>
        /// Optimizes a SQL query that would be executed with Dapper
        /// </summary>
        /// <param name="connection">The database connection</param>
        /// <param name="sql">The SQL query to optimize</param>
        /// <param name="queryAnalyzer">The query analyzer service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optimized SQL query</returns>
        public static async Task<string> OptimizeQueryAsync(
            this IDbConnection connection,
            string sql,
            IQueryAnalyzer queryAnalyzer,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            if (queryAnalyzer == null)
                throw new ArgumentNullException(nameof(queryAnalyzer));

            // Get the connection string
            string connectionString = GetConnectionString(connection);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Unable to get connection string from IDbConnection");

            // Optimize the query
            return await queryAnalyzer.OptimizeQueryAsync(sql, connectionString, cancellationToken);
        }

        /// <summary>
        /// Benchmarks a SQL query that would be executed with Dapper
        /// </summary>
        /// <param name="connection">The database connection</param>
        /// <param name="sql">The SQL query to benchmark</param>
        /// <param name="queryAnalyzer">The query analyzer service</param>
        /// <param name="iterations">Number of iterations to run</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Performance metrics for the query</returns>
        public static async Task<QueryPerformanceMetrics> BenchmarkQueryAsync(
            this IDbConnection connection,
            string sql,
            IQueryAnalyzer queryAnalyzer,
            int iterations = 3,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            if (queryAnalyzer == null)
                throw new ArgumentNullException(nameof(queryAnalyzer));

            // Get the connection string
            string connectionString = GetConnectionString(connection);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Unable to get connection string from IDbConnection");

            // Benchmark the query
            return await queryAnalyzer.BenchmarkQueryAsync(sql, connectionString, iterations, cancellationToken);
        }

        /// <summary>
        /// Gets the connection string from an IDbConnection
        /// </summary>
        private static string GetConnectionString(IDbConnection connection)
        {
            if (connection == null)
                return null;

            // If the connection already has a connection string property, use it
            var connectionStringProperty = connection.GetType().GetProperty("ConnectionString");
            if (connectionStringProperty != null)
            {
                return connectionStringProperty.GetValue(connection) as string;
            }

            // If it's a DbConnection, we can get the connection string directly
            if (connection is DbConnection dbConnection)
            {
                return dbConnection.ConnectionString;
            }

            // Otherwise, return null as we can't determine the connection string
            return null;
        }
    }
}