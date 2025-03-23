using System.Threading;
using System.Threading.Tasks;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Interfaces
{
    /// <summary>
    /// Interface for analyzing SQL queries and providing optimization suggestions
    /// </summary>
    public interface IQueryAnalyzer
    {
        /// <summary>
        /// Analyzes a SQL query and provides optimization suggestions
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Analysis result with optimization suggestions</returns>
        Task<QueryAnalysisResult> AnalyzeQueryAsync(string query, string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimizes a SQL query automatically
        /// </summary>
        /// <param name="query">The SQL query to optimize</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optimized SQL query</returns>
        Task<string> OptimizeQueryAsync(string query, string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Benchmarks the performance of a SQL query
        /// </summary>
        /// <param name="query">The SQL query to benchmark</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="iterations">Number of iterations to run</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Performance metrics for the query</returns>
        Task<QueryPerformanceMetrics> BenchmarkQueryAsync(string query, string connectionString, int iterations = 3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Compares the performance of two SQL queries
        /// </summary>
        /// <param name="originalQuery">The original SQL query</param>
        /// <param name="optimizedQuery">The optimized SQL query</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="iterations">Number of iterations to run</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Performance comparison result</returns>
        Task<QueryPerformanceComparison> CompareQueriesAsync(string originalQuery, string optimizedQuery, string connectionString, int iterations = 3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes a SQL query and provides optimization suggestions, with specific analysis options
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="options">Analysis options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Analysis result with optimization suggestions</returns>
        Task<QueryAnalysisResult> AnalyzeQueryAsync(string query, string connectionString, QueryAnalysisOptions options, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a performance comparison between two SQL queries
    /// </summary>
    public class QueryPerformanceComparison
    {
        /// <summary>
        /// The original SQL query
        /// </summary>
        public string OriginalQuery { get; set; }

        /// <summary>
        /// The optimized SQL query
        /// </summary>
        public string OptimizedQuery { get; set; }

        /// <summary>
        /// Performance metrics for the original query
        /// </summary>
        public QueryPerformanceMetrics OriginalMetrics { get; set; }

        /// <summary>
        /// Performance metrics for the optimized query
        /// </summary>
        public QueryPerformanceMetrics OptimizedMetrics { get; set; }

        /// <summary>
        /// Percentage improvement in execution time
        /// </summary>
        public double ExecutionTimeImprovement => CalculateImprovement(OriginalMetrics.ExecutionTimeMs, OptimizedMetrics.ExecutionTimeMs);

        /// <summary>
        /// Percentage improvement in CPU time
        /// </summary>
        public double CpuTimeImprovement => CalculateImprovement(OriginalMetrics.CpuTimeMs, OptimizedMetrics.CpuTimeMs);

        /// <summary>
        /// Percentage improvement in logical reads
        /// </summary>
        public double LogicalReadsImprovement => CalculateImprovement(OriginalMetrics.LogicalReads, OptimizedMetrics.LogicalReads);

        private double CalculateImprovement(double original, double optimized)
        {
            if (original <= 0) return 0;
            return ((original - optimized) / original) * 100;
        }
    }

    /// <summary>
    /// Options for query analysis
    /// </summary>
    public class QueryAnalysisOptions
    {
        /// <summary>
        /// Whether to analyze execution plan
        /// </summary>
        public bool AnalyzeExecutionPlan { get; set; } = true;

        /// <summary>
        /// Whether to analyze indexes
        /// </summary>
        public bool AnalyzeIndexes { get; set; } = true;

        /// <summary>
        /// Whether to analyze query syntax and structure
        /// </summary>
        public bool AnalyzeSyntax { get; set; } = true;

        /// <summary>
        /// Whether to perform actual execution for performance metrics
        /// </summary>
        public bool CollectPerformanceMetrics { get; set; } = true;

        /// <summary>
        /// Whether to collect statistics data
        /// </summary>
        public bool CollectStatistics { get; set; } = true;

        /// <summary>
        /// Maximum execution time allowed in milliseconds (0 = unlimited)
        /// </summary>
        public int MaxExecutionTimeMs { get; set; } = 30000;

        /// <summary>
        /// Whether to attempt to rewrite the query
        /// </summary>
        public bool AttemptQueryRewrite { get; set; } = true;

        /// <summary>
        /// Maximum number of recommendations to generate
        /// </summary>
        public int MaxRecommendations { get; set; } = 10;
    }
}