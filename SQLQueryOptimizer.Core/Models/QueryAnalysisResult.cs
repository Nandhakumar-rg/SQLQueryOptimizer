using System;
using System.Collections.Generic;

namespace SQLQueryOptimizer.Core.Models
{
    /// <summary>
    /// Represents the result of a SQL query analysis
    /// </summary>
    public class QueryAnalysisResult
    {
        /// <summary>
        /// The original SQL query that was analyzed
        /// </summary>
        public string OriginalQuery { get; set; }

        /// <summary>
        /// Suggested optimized version of the query
        /// </summary>
        public string OptimizedQuery { get; set; }

        /// <summary>
        /// Performance metrics comparing original and optimized queries
        /// </summary>
        public QueryPerformanceMetrics PerformanceMetrics { get; set; }

        /// <summary>
        /// List of optimization suggestions with explanations
        /// </summary>
        public List<OptimizationSuggestion> Suggestions { get; set; } = new List<OptimizationSuggestion>();

        /// <summary>
        /// List of recommended indexes to improve query performance
        /// </summary>
        public List<IndexRecommendation> IndexRecommendations { get; set; } = new List<IndexRecommendation>();

        /// <summary>
        /// Detected issues in the original query
        /// </summary>
        public List<QueryIssue> DetectedIssues { get; set; } = new List<QueryIssue>();

        /// <summary>
        /// Estimated performance improvement percentage between original and optimized queries
        /// </summary>
        public double EstimatedImprovementPercentage { get; set; }

        /// <summary>
        /// Timestamp when the analysis was performed
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Database server version information
        /// </summary>
        public string DatabaseServerInfo { get; set; }

        /// <summary>
        /// Complexity rating of the query (1-10)
        /// </summary>
        public int ComplexityRating { get; set; }
    }

    /// <summary>
    /// Represents a specific optimization suggestion for a SQL query
    /// </summary>
    public class OptimizationSuggestion
    {
        /// <summary>
        /// Short title of the suggestion
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Detailed explanation of the suggestion
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The original problematic part of the query
        /// </summary>
        public string OriginalQueryPart { get; set; }

        /// <summary>
        /// The suggested replacement for the problematic part
        /// </summary>
        public string SuggestedQueryPart { get; set; }

        /// <summary>
        /// Priority level of the suggestion (Critical, High, Medium, Low)
        /// </summary>
        public SuggestionPriority Priority { get; set; }

        /// <summary>
        /// Estimated impact on performance (percentage improvement)
        /// </summary>
        public double EstimatedImpact { get; set; }
    }

    /// <summary>
    /// Priority levels for optimization suggestions
    /// </summary>
    public enum SuggestionPriority
    {
        Critical,
        High,
        Medium,
        Low
    }

    /// <summary>
    /// Represents an identified issue in a SQL query
    /// </summary>
    public class QueryIssue
    {
        /// <summary>
        /// Type of issue detected
        /// </summary>
        public QueryIssueType IssueType { get; set; }

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The problematic part of the query
        /// </summary>
        public string QueryPart { get; set; }

        /// <summary>
        /// Line number where the issue was detected (if available)
        /// </summary>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Position in the line where the issue starts (if available)
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// Severity of the issue (Critical, Major, Minor, Info)
        /// </summary>
        public IssueSeverity Severity { get; set; }
    }

    /// <summary>
    /// Types of issues that can be detected in SQL queries
    /// </summary>
    public enum QueryIssueType
    {
        CartesianProduct,
        ColumnWildcard,
        ImplicitConversion,
        MissingIndex,
        NonSargableCondition,
        ParameterSniffing,
        SuboptimalJoin,
        TableScan,
        UnnecessaryDistinct,
        ScalarFunction
    }

    /// <summary>
    /// Severity levels for query issues
    /// </summary>
    public enum IssueSeverity
    {
        Critical,
        Major,
        Medium,
        Minor,
        Info
    }
}