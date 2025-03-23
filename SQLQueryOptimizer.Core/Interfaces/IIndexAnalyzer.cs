using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Interfaces
{
    /// <summary>
    /// Interface for analyzing and recommending indexes for SQL queries
    /// </summary>
    public interface IIndexAnalyzer
    {
        /// <summary>
        /// Analyzes a SQL query and suggests indexes that could improve its performance
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of index recommendations</returns>
        Task<List<IndexRecommendation>> AnalyzeQueryForIndexesAsync(string query, string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes existing indexes on tables referenced in the query to find redundant or unused indexes
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of redundant or unused indexes that could be removed</returns>
        Task<List<RedundantIndexInfo>> FindRedundantIndexesAsync(string query, string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates the impact of creating a recommended index on query performance
        /// </summary>
        /// <param name="query">The SQL query</param>
        /// <param name="indexRecommendation">The index recommendation to evaluate</param>
        /// <param name="connectionString">Connection string to the database</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Estimated performance impact in percentage</returns>
        Task<double> EstimateIndexImpactAsync(string query, IndexRecommendation indexRecommendation, string connectionString, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents information about a redundant or unused index
    /// </summary>
    public class RedundantIndexInfo
    {
        /// <summary>
        /// Database name where the index exists
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Schema name of the table
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Table name that contains the index
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Name of the index
        /// </summary>
        public string IndexName { get; set; }

        /// <summary>
        /// Reason why the index is considered redundant or unused
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Recommendation for what to do with the index (e.g., Remove, Replace, Merge)
        /// </summary>
        public string Recommendation { get; set; }

        /// <summary>
        /// DDL statement to drop the index
        /// </summary>
        public string DropIndexStatement { get; set; }

        /// <summary>
        /// Size of the index in MB
        /// </summary>
        public double SizeMB { get; set; }

        /// <summary>
        /// Number of times the index has been used since last server restart
        /// </summary>
        public long UsageCount { get; set; }

        /// <summary>
        /// Date when the index was last used
        /// </summary>
        public System.DateTime? LastUsedDate { get; set; }
    }
}