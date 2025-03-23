using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SQLQueryOptimizer.Core.Analyzers;
using SQLQueryOptimizer.Core.Interfaces;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Services
{
    /// <summary>
    /// Implementation of the IQueryAnalyzer for SQL Server
    /// </summary>
    public class SqlQueryAnalyzer : IQueryAnalyzer
    {
        private readonly ILogger<SqlQueryAnalyzer> _logger;
        private readonly IQueryPerformanceCollector _performanceCollector;
        private readonly IIndexAnalyzer _indexAnalyzer;
        private readonly QueryPatternAnalyzer _patternAnalyzer;

        /// <summary>
        /// Initializes a new instance of the SqlQueryAnalyzer
        /// </summary>
        public SqlQueryAnalyzer(
            ILogger<SqlQueryAnalyzer> logger,
            IQueryPerformanceCollector performanceCollector,
            IIndexAnalyzer indexAnalyzer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceCollector = performanceCollector ?? throw new ArgumentNullException(nameof(performanceCollector));
            _indexAnalyzer = indexAnalyzer ?? throw new ArgumentNullException(nameof(indexAnalyzer));
            _patternAnalyzer = new QueryPatternAnalyzer();
        }

        /// <inheritdoc />
        public async Task<QueryAnalysisResult> AnalyzeQueryAsync(string query, string connectionString, CancellationToken cancellationToken = default)
        {
            return await AnalyzeQueryAsync(query, connectionString, new QueryAnalysisOptions(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<QueryAnalysisResult> AnalyzeQueryAsync(
            string query,
            string connectionString,
            QueryAnalysisOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Starting analysis of SQL query");

            var result = new QueryAnalysisResult
            {
                OriginalQuery = query,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // Get database server info
                result.DatabaseServerInfo = await GetDatabaseServerInfoAsync(connectionString, cancellationToken);

                // Analyze query patterns
                result.DetectedIssues = _patternAnalyzer.AnalyzeQuery(query);

                // Analyze performance if requested
                if (options.CollectPerformanceMetrics)
                {
                    _logger.LogInformation("Collecting performance metrics");
                    result.PerformanceMetrics = await _performanceCollector.CollectMetricsAsync(query, connectionString, cancellationToken);
                }

                // Analyze indexes if requested
                if (options.AnalyzeIndexes)
                {
                    _logger.LogInformation("Analyzing indexes");
                    result.IndexRecommendations = await _indexAnalyzer.AnalyzeQueryForIndexesAsync(query, connectionString, cancellationToken);
                }

                // Generate optimized query if requested
                if (options.AttemptQueryRewrite)
                {
                    _logger.LogInformation("Attempting to optimize query");
                    result.OptimizedQuery = await OptimizeQueryInternalAsync(query, result.DetectedIssues, cancellationToken);

                    // If we have an optimized query, compare performance
                    if (!string.IsNullOrWhiteSpace(result.OptimizedQuery) && options.CollectPerformanceMetrics)
                    {
                        var comparison = await CompareQueriesAsync(query, result.OptimizedQuery, connectionString, 3, cancellationToken);
                        result.EstimatedImprovementPercentage = comparison.ExecutionTimeImprovement;
                    }
                }

                // Calculate complexity rating based on various factors
                result.ComplexityRating = CalculateComplexityRating(query, result.DetectedIssues);

                // Generate optimization suggestions
                result.Suggestions = GenerateOptimizationSuggestions(result.DetectedIssues, result.IndexRecommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing SQL query");
                throw;
            }

            _logger.LogInformation("Query analysis completed");
            return result;
        }

        /// <inheritdoc />
        public async Task<string> OptimizeQueryAsync(string query, string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var issues = _patternAnalyzer.AnalyzeQuery(query);
            return await OptimizeQueryInternalAsync(query, issues, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<QueryPerformanceMetrics> BenchmarkQueryAsync(
            string query,
            string connectionString,
            int iterations = 3,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            return await _performanceCollector.BenchmarkQueryAsync(query, connectionString, iterations, 1, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<QueryPerformanceComparison> CompareQueriesAsync(
            string originalQuery,
            string optimizedQuery,
            string connectionString,
            int iterations = 3,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(originalQuery))
                throw new ArgumentNullException(nameof(originalQuery));

            if (string.IsNullOrWhiteSpace(optimizedQuery))
                throw new ArgumentNullException(nameof(optimizedQuery));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Comparing performance of original and optimized queries");

            var originalMetrics = await _performanceCollector.BenchmarkQueryAsync(
                originalQuery, connectionString, iterations, 1, cancellationToken);

            var optimizedMetrics = await _performanceCollector.BenchmarkQueryAsync(
                optimizedQuery, connectionString, iterations, 1, cancellationToken);

            var comparison = new QueryPerformanceComparison
            {
                OriginalQuery = originalQuery,
                OptimizedQuery = optimizedQuery,
                OriginalMetrics = originalMetrics,
                OptimizedMetrics = optimizedMetrics
            };

            return comparison;
        }

        private async Task<string> GetDatabaseServerInfoAsync(string connectionString, CancellationToken cancellationToken)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT SERVERPROPERTY('ProductVersion') AS Version, " +
                                             "SERVERPROPERTY('ProductLevel') AS Level, " +
                                             "SERVERPROPERTY('Edition') AS Edition";

                        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                        {
                            if (await reader.ReadAsync(cancellationToken))
                            {
                                string version = reader["Version"].ToString();
                                string level = reader["Level"].ToString();
                                string edition = reader["Edition"].ToString();

                                return $"SQL Server {edition} {version} {level}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting database server info");
            }

            return "Unknown";
        }

        private async Task<string> OptimizeQueryInternalAsync(string query, List<QueryIssue> issues, CancellationToken cancellationToken)
        {
            // This is a placeholder for query optimization logic
            // In a real implementation, we would use more sophisticated techniques like
            // parse trees, query transformation rules, etc.
            string optimizedQuery = query;

            // Simple replacements based on common issues
            foreach (var issue in issues)
            {
                switch (issue.IssueType)
                {
                    case QueryIssueType.ColumnWildcard:
                        // Don't try to replace with specific columns since we don't know the schema
                        // Just leave the query as is for now
                        // optimizedQuery = optimizedQuery.Replace("SELECT *", "SELECT [column1], [column2], [column3]");
                        break;

                    case QueryIssueType.UnnecessaryDistinct:
                        // This is a simplified approach - would need to analyze the query more carefully in practice
                        optimizedQuery = optimizedQuery.Replace("SELECT DISTINCT", "SELECT");
                        break;

                        // Add more optimization rules here
                }
            }

            // Return original query if no optimizations were made
            return string.Equals(query, optimizedQuery) ? query : optimizedQuery;
        }

        private int CalculateComplexityRating(string query, List<QueryIssue> issues)
        {
            // This is a simple heuristic for complexity rating from 1-10
            int baseComplexity = 1;

            // Add complexity based on query length
            baseComplexity += Math.Min(3, query.Length / 1000);

            // Add complexity based on detected issues
            baseComplexity += Math.Min(4, issues.Count / 2);

            // Add complexity based on query patterns
            if (query.Contains("JOIN", StringComparison.OrdinalIgnoreCase))
                baseComplexity += 1;

            if (query.Contains("OUTER APPLY", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("CROSS APPLY", StringComparison.OrdinalIgnoreCase))
                baseComplexity += 2;

            if (query.Contains("CTE", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("WITH ", StringComparison.OrdinalIgnoreCase))
                baseComplexity += 1;

            if (query.Contains("UNION", StringComparison.OrdinalIgnoreCase))
                baseComplexity += 1;

            if (query.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase))
                baseComplexity += 1;

            // Cap at 10
            return Math.Min(10, baseComplexity);
        }

        private List<OptimizationSuggestion> GenerateOptimizationSuggestions(
            List<QueryIssue> issues,
            List<IndexRecommendation> indexRecommendations)
        {
            var suggestions = new List<OptimizationSuggestion>();

            // Convert issues to suggestions
            foreach (var issue in issues)
            {
                var priority = ConvertSeverityToPriority(issue.Severity);

                var suggestion = new OptimizationSuggestion
                {
                    Title = GetTitleForIssueType(issue.IssueType),
                    Description = issue.Description,
                    OriginalQueryPart = issue.QueryPart,
                    Priority = priority,
                    // We would fill in SuggestedQueryPart with a proper replacement in a real implementation
                    EstimatedImpact = EstimateImpactForIssueType(issue.IssueType)
                };

                suggestions.Add(suggestion);
            }

            // Convert index recommendations to suggestions
            foreach (var recommendation in indexRecommendations ?? new List<IndexRecommendation>())
            {
                var priority = GetPriorityForIndexImpact(recommendation.EstimatedImpact);

                var suggestion = new OptimizationSuggestion
                {
                    Title = $"Create index on {recommendation.TableName}",
                    Description = $"Creating an index on {string.Join(", ", recommendation.KeyColumns)} could improve query performance by approximately {recommendation.EstimatedImpact:F1}%.",
                    SuggestedQueryPart = recommendation.CreateIndexStatement,
                    Priority = priority,
                    EstimatedImpact = recommendation.EstimatedImpact
                };

                suggestions.Add(suggestion);
            }

            // Sort by estimated impact (higher impact first)
            suggestions.Sort((a, b) => b.EstimatedImpact.CompareTo(a.EstimatedImpact));

            return suggestions;
        }

        private SuggestionPriority ConvertSeverityToPriority(IssueSeverity severity)
        {
            return severity switch
            {
                IssueSeverity.Critical => SuggestionPriority.Critical,
                IssueSeverity.Major => SuggestionPriority.High,
                IssueSeverity.Medium => SuggestionPriority.Medium,
                _ => SuggestionPriority.Low
            };
        }

        private string GetTitleForIssueType(QueryIssueType issueType)
        {
            return issueType switch
            {
                QueryIssueType.CartesianProduct => "Avoid cartesian product",
                QueryIssueType.ColumnWildcard => "Avoid SELECT *",
                QueryIssueType.ImplicitConversion => "Avoid implicit type conversion",
                QueryIssueType.MissingIndex => "Add missing index",
                QueryIssueType.NonSargableCondition => "Use SARGable conditions",
                QueryIssueType.ParameterSniffing => "Handle parameter sniffing",
                QueryIssueType.SuboptimalJoin => "Optimize join strategy",
                QueryIssueType.TableScan => "Avoid table scans",
                QueryIssueType.UnnecessaryDistinct => "Avoid unnecessary DISTINCT",
                QueryIssueType.ScalarFunction => "Avoid scalar functions",
                _ => "Optimize query"
            };
        }

        private double EstimateImpactForIssueType(QueryIssueType issueType)
        {
            // These are rough estimates and would vary based on query and database
            return issueType switch
            {
                QueryIssueType.CartesianProduct => 80.0,
                QueryIssueType.ColumnWildcard => 30.0,
                QueryIssueType.ImplicitConversion => 60.0,
                QueryIssueType.MissingIndex => 70.0,
                QueryIssueType.NonSargableCondition => 50.0,
                QueryIssueType.ParameterSniffing => 40.0,
                QueryIssueType.SuboptimalJoin => 45.0,
                QueryIssueType.TableScan => 65.0,
                QueryIssueType.UnnecessaryDistinct => 25.0,
                QueryIssueType.ScalarFunction => 55.0,
                _ => 10.0
            };
        }

        private SuggestionPriority GetPriorityForIndexImpact(double impact)
        {
            if (impact >= 70.0)
                return SuggestionPriority.Critical;
            if (impact >= 40.0)
                return SuggestionPriority.High;
            if (impact >= 20.0)
                return SuggestionPriority.Medium;
            return SuggestionPriority.Low;
        }
    }
}