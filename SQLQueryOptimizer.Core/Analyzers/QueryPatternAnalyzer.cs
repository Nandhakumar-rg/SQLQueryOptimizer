using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Analyzers
{
    /// <summary>
    /// Analyzes SQL query patterns to identify potential issues
    /// </summary>
    public class QueryPatternAnalyzer
    {
        /// <summary>
        /// Analyzes a SQL query for common issues and anti-patterns
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <returns>List of detected issues</returns>
        public List<QueryIssue> AnalyzeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var issues = new List<QueryIssue>();

            // Normalize query to remove comments and extra whitespace
            string normalizedQuery = NormalizeQuery(query);

            // Check for SELECT *
            CheckForSelectStar(normalizedQuery, issues);

            // Check for NOLOCK hint
            CheckForNoLock(normalizedQuery, issues);

            // Check for implicit conversions
            CheckForImplicitConversions(normalizedQuery, issues);

            // Check for non-SARGable conditions
            CheckForNonSargableConditions(normalizedQuery, issues);

            // Check for functions on columns in WHERE clauses
            CheckForFunctionsOnColumns(normalizedQuery, issues);

            // Check for DISTINCT usage
            CheckForDistinct(normalizedQuery, issues);

            // Check for potential cartesian products
            CheckForCartesianProducts(normalizedQuery, issues);

            // Check for scalar functions
            CheckForScalarFunctions(normalizedQuery, issues);

            return issues;
        }

        private string NormalizeQuery(string query)
        {
            // Remove comments
            query = Regex.Replace(query, @"--.*$", "", RegexOptions.Multiline);
            query = Regex.Replace(query, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // Normalize whitespace
            query = Regex.Replace(query, @"\s+", " ");

            return query.Trim();
        }

        private void CheckForSelectStar(string query, List<QueryIssue> issues)
        {
            // Regular expression to match SELECT * (but not SELECT COUNT(*))
            if (Regex.IsMatch(query, @"\bSELECT\s+\*\s+FROM\b", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.ColumnWildcard,
                    Description = "Using SELECT * returns all columns, which can lead to unnecessary I/O and network traffic. Specify only the columns you need.",
                    QueryPart = "SELECT *",
                    Severity = IssueSeverity.Medium
                });
            }
        }

        private void CheckForNoLock(string query, List<QueryIssue> issues)
        {
            if (Regex.IsMatch(query, @"\bWITH\s*\(\s*NOLOCK\s*\)", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.TableScan,
                    Description = "NOLOCK hint can lead to dirty reads and inconsistent results. Consider using an appropriate isolation level instead.",
                    QueryPart = "WITH (NOLOCK)",
                    Severity = IssueSeverity.Medium
                });
            }
        }

        private void CheckForImplicitConversions(string query, List<QueryIssue> issues)
        {
            // This is a simplified check for potential implicit conversions
            // In a real implementation, we would need to know the schema to be accurate
            if (Regex.IsMatch(query, @"\b(VARCHAR|NVARCHAR|CHAR|NCHAR)\b.+\b(=|<|>|<=|>=)\b.+\b(INT|BIGINT|SMALLINT|TINYINT|DECIMAL|NUMERIC)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(query, @"\b(INT|BIGINT|SMALLINT|TINYINT|DECIMAL|NUMERIC)\b.+\b(=|<|>|<=|>=)\b.+\b(VARCHAR|NVARCHAR|CHAR|NCHAR)\b", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.ImplicitConversion,
                    Description = "Potential implicit data type conversion detected. This can prevent index usage and lead to table scans.",
                    QueryPart = "Potential comparison between string and numeric types",
                    Severity = IssueSeverity.Major
                });
            }
        }

        private void CheckForNonSargableConditions(string query, List<QueryIssue> issues)
        {
            // Check for functions applied to columns in WHERE clauses
            if (Regex.IsMatch(query, @"\bWHERE\b.+\b(UPPER|LOWER|SUBSTRING|LEFT|RIGHT|LTRIM|RTRIM|DATEPART|YEAR|MONTH|DAY|CONVERT|CAST)\s*\(.*?\)", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.NonSargableCondition,
                    Description = "Using functions on columns in WHERE clause prevents index usage. Try to apply functions to parameters instead.",
                    QueryPart = "Function applied to column in WHERE clause",
                    Severity = IssueSeverity.Major
                });
            }

            // Check for leading wildcards in LIKE
            if (Regex.IsMatch(query, @"\bLIKE\s+[N'""]*%", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.NonSargableCondition,
                    Description = "Leading wildcard in LIKE clause prevents efficient index usage. Consider using full-text search instead.",
                    QueryPart = "LIKE '%...'",
                    Severity = IssueSeverity.Medium
                });
            }
        }

        private void CheckForFunctionsOnColumns(string query, List<QueryIssue> issues)
        {
            // Already covered in CheckForNonSargableConditions
        }

        private void CheckForDistinct(string query, List<QueryIssue> issues)
        {
            if (Regex.IsMatch(query, @"\bSELECT\s+DISTINCT\b", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.UnnecessaryDistinct,
                    Description = "DISTINCT can be expensive. Consider using appropriate JOINs or GROUP BY instead if possible.",
                    QueryPart = "SELECT DISTINCT",
                    Severity = IssueSeverity.Minor
                });
            }
        }

        private void CheckForCartesianProducts(string query, List<QueryIssue> issues)
        {
            // Check for multiple FROM tables without a JOIN condition
            if (Regex.IsMatch(query, @"\bFROM\s+\w+\s*,\s*\w+(?!\s+ON|\s+INNER JOIN|\s+LEFT JOIN|\s+RIGHT JOIN|\s+FULL JOIN)", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.CartesianProduct,
                    Description = "Potential cartesian product detected. Make sure all tables are properly joined.",
                    QueryPart = "FROM table1, table2 without JOIN condition",
                    Severity = IssueSeverity.Critical
                });
            }
        }

        private void CheckForScalarFunctions(string query, List<QueryIssue> issues)
        {
            if (Regex.IsMatch(query, @"\bSELECT\b.+\bdbo\.\w+\(", RegexOptions.IgnoreCase))
            {
                issues.Add(new QueryIssue
                {
                    IssueType = QueryIssueType.ScalarFunction,
                    Description = "Scalar functions in SELECT can perform poorly. Consider inline logic or computed columns.",
                    QueryPart = "Scalar function in SELECT clause",
                    Severity = IssueSeverity.Medium
                });
            }
        }
    }
}