using System.Threading;
using System.Threading.Tasks;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Interfaces
{
    /// <summary>
    /// Interface for collecting performance metrics for SQL queries
    /// </summary>
    public interface IQueryPerformanceCollector
    {
        /// <summary>
        /// Collects performance metrics for a SQL query
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Performance metrics for the query</returns>
        Task<QueryPerformanceMetrics> CollectMetricsAsync(string query, string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a performance benchmark for a SQL query with multiple iterations
        /// </summary>
        /// <param name="query">The SQL query to benchmark</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="iterations">Number of iterations to run</param>
        /// <param name="warmupIterations">Number of warmup iterations to run before collecting metrics</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated performance metrics across all iterations</returns>
        Task<QueryPerformanceMetrics> BenchmarkQueryAsync(
            string query,
            string connectionString,
            int iterations = 3,
            int warmupIterations = 1,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the execution plan for a SQL query
        /// </summary>
        /// <param name="query">The SQL query</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="actualExecutionPlan">Whether to get the actual (true) or estimated (false) execution plan</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The execution plan in XML format</returns>
        Task<string> GetExecutionPlanAsync(
            string query,
            string connectionString,
            bool actualExecutionPlan = true,
            CancellationToken cancellationToken = default);
    }
}