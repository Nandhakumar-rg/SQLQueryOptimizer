using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SQLQueryOptimizer.Core.Interfaces;
using SQLQueryOptimizer.Core.Models;

namespace SQLQueryOptimizer.Core.Services
{
    /// <summary>
    /// Implementation of IIndexAnalyzer for SQL Server
    /// </summary>
    public class SqlIndexAnalyzer : IIndexAnalyzer
    {
        private readonly ILogger<SqlIndexAnalyzer> _logger;
        private readonly IQueryPerformanceCollector _performanceCollector;

        /// <summary>
        /// Initializes a new instance of the SqlIndexAnalyzer
        /// </summary>
        public SqlIndexAnalyzer(
            ILogger<SqlIndexAnalyzer> logger,
            IQueryPerformanceCollector performanceCollector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceCollector = performanceCollector ?? throw new ArgumentNullException(nameof(performanceCollector));
        }

        /// <inheritdoc />
        public async Task<List<IndexRecommendation>> AnalyzeQueryForIndexesAsync(
            string query,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Analyzing query for index recommendations");

            var recommendations = new List<IndexRecommendation>();

            try
            {
                // Get execution plan to analyze for missing indexes
                string executionPlan = await _performanceCollector.GetExecutionPlanAsync(
                    query, connectionString, false, cancellationToken);

                if (string.IsNullOrWhiteSpace(executionPlan))
                {
                    _logger.LogWarning("Unable to get execution plan for index analysis");
                    return recommendations;
                }

                // Extract missing index information from the execution plan
                recommendations = ExtractMissingIndexesFromPlan(executionPlan);

                // Generate CREATE INDEX statements
                foreach (var recommendation in recommendations)
                {
                    recommendation.CreateIndexStatement = GenerateCreateIndexStatement(recommendation);

                    // Estimate impact if not already set
                    if (recommendation.EstimatedImpact <= 0)
                    {
                        recommendation.EstimatedImpact = await EstimateIndexImpactAsync(
                            query, recommendation, connectionString, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing query for index recommendations");
            }

            return recommendations;
        }

        /// <inheritdoc />
        public async Task<List<RedundantIndexInfo>> FindRedundantIndexesAsync(
            string query,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Finding redundant indexes for tables used in query");

            var redundantIndexes = new List<RedundantIndexInfo>();

            try
            {
                // Extract table names from the query
                var tableNames = ExtractTableNamesFromQuery(query);

                if (tableNames.Count == 0)
                {
                    _logger.LogWarning("No table names found in query");
                    return redundantIndexes;
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    // For each table, find potentially redundant indexes
                    foreach (var table in tableNames)
                    {
                        var tableRedundantIndexes = await FindRedundantIndexesForTableAsync(
                            connection, table, cancellationToken);

                        redundantIndexes.AddRange(tableRedundantIndexes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding redundant indexes");
            }

            return redundantIndexes;
        }

        /// <inheritdoc />
        public async Task<double> EstimateIndexImpactAsync(
            string query,
            IndexRecommendation indexRecommendation,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (indexRecommendation == null)
                throw new ArgumentNullException(nameof(indexRecommendation));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Estimating impact of index recommendation");

            try
            {
                // In a real implementation, we would create a temporary index or use SQL Server's Database Engine Tuning Advisor
                // For this implementation, we'll use a simple heuristic based on the index recommendation

                // We'll use the "improvement" value provided by SQL Server in the execution plan
                // as a starting point, or make a rough estimate based on the columns

                // If the estimated impact is already set, return it
                if (indexRecommendation.EstimatedImpact > 0)
                    return indexRecommendation.EstimatedImpact;

                // Simple heuristic based on key column count and table
                double baseImpact = 25.0; // Base impact percentage

                // More key columns generally means more specific index, potentially higher impact
                baseImpact += Math.Min(25.0, indexRecommendation.KeyColumns.Count * 5.0);

                // Included columns can reduce lookups
                baseImpact += Math.Min(15.0, indexRecommendation.IncludedColumns.Count * 3.0);

                // Cap at 90% to be realistic
                return Math.Min(90.0, baseImpact);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating index impact");
                return 0;
            }
        }

        private List<IndexRecommendation> ExtractMissingIndexesFromPlan(string executionPlan)
        {
            var recommendations = new List<IndexRecommendation>();

            if (string.IsNullOrWhiteSpace(executionPlan))
                return recommendations;

            try
            {
                // This is a simplified implementation using regex
                // In a real implementation, we would use XML parsing to extract missing index information

                // Example pattern for missing index in execution plan XML
                var missingIndexPattern = @"<MissingIndex.+?Database=\[([^\]]+)\].+?Schema=\[([^\]]+)\].+?Table=\[([^\]]+)\]";
                var matches = Regex.Matches(executionPlan, missingIndexPattern, RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count < 4)
                        continue;

                    var recommendation = new IndexRecommendation
                    {
                        DatabaseName = match.Groups[1].Value,
                        SchemaName = match.Groups[2].Value,
                        TableName = match.Groups[3].Value,
                        IndexType = IndexType.Nonclustered,
                        IsUnique = false
                    };

                    // Extract key and included columns
                    ExtractColumnsFromPlan(executionPlan, recommendation, match.Index);

                    // Extract impact if available
                    double impact = ExtractImpactFromPlan(executionPlan, match.Index);
                    if (impact > 0)
                    {
                        recommendation.EstimatedImpact = impact;
                    }

                    recommendations.Add(recommendation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting missing indexes from execution plan");
            }

            return recommendations;
        }

        private void ExtractColumnsFromPlan(string executionPlan, IndexRecommendation recommendation, int startIndex)
        {
            try
            {
                // Extract equality columns (most selective, should be first in index)
                var equalityColumnsPattern = @"<ColumnGroup Usage=""EQUALITY"">(.+?)</ColumnGroup>";
                var equalityMatch = Regex.Match(executionPlan.Substring(startIndex), equalityColumnsPattern, RegexOptions.Singleline);
                if (equalityMatch.Success)
                {
                    var columnPattern = @"<Column.+?Name=\[([^\]]+)\]";
                    var columnMatches = Regex.Matches(equalityMatch.Groups[1].Value, columnPattern);

                    foreach (Match columnMatch in columnMatches)
                    {
                        recommendation.KeyColumns.Add(new IndexColumn
                        {
                            ColumnName = columnMatch.Groups[1].Value,
                            SortDirection = SortDirection.Ascending
                        });
                    }
                }

                // Extract inequality columns (range predicates, should be after equality columns)
                var inequalityColumnsPattern = @"<ColumnGroup Usage=""INEQUALITY"">(.+?)</ColumnGroup>";
                var inequalityMatch = Regex.Match(executionPlan.Substring(startIndex), inequalityColumnsPattern, RegexOptions.Singleline);
                if (inequalityMatch.Success)
                {
                    var columnPattern = @"<Column.+?Name=\[([^\]]+)\]";
                    var columnMatches = Regex.Matches(inequalityMatch.Groups[1].Value, columnPattern);

                    foreach (Match columnMatch in columnMatches)
                    {
                        recommendation.KeyColumns.Add(new IndexColumn
                        {
                            ColumnName = columnMatch.Groups[1].Value,
                            SortDirection = SortDirection.Ascending
                        });
                    }
                }

                // Extract included columns (columns needed but not part of the key)
                var includedColumnsPattern = @"<ColumnGroup Usage=""INCLUDE"">(.+?)</ColumnGroup>";
                var includedMatch = Regex.Match(executionPlan.Substring(startIndex), includedColumnsPattern, RegexOptions.Singleline);
                if (includedMatch.Success)
                {
                    var columnPattern = @"<Column.+?Name=\[([^\]]+)\]";
                    var columnMatches = Regex.Matches(includedMatch.Groups[1].Value, columnPattern);

                    foreach (Match columnMatch in columnMatches)
                    {
                        recommendation.IncludedColumns.Add(new IndexColumn
                        {
                            ColumnName = columnMatch.Groups[1].Value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting columns from execution plan");
            }
        }

        private double ExtractImpactFromPlan(string executionPlan, int startIndex)
        {
            try
            {
                var impactPattern = @"<MissingIndex.+?Impact=""([^""]+)""";
                var match = Regex.Match(executionPlan.Substring(startIndex), impactPattern);

                if (match.Success && match.Groups.Count > 1)
                {
                    if (double.TryParse(match.Groups[1].Value, out double impact))
                    {
                        return impact;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting impact from execution plan");
            }

            return 0;
        }

        private string GenerateCreateIndexStatement(IndexRecommendation recommendation)
        {
            if (recommendation == null || recommendation.KeyColumns.Count == 0)
                return string.Empty;

            try
            {
                var indexName = recommendation.SuggestIndexName();
                var sb = new System.Text.StringBuilder();

                sb.Append($"CREATE ");

                if (recommendation.IsUnique)
                    sb.Append("UNIQUE ");

                sb.Append(recommendation.IndexType == IndexType.Clustered ? "CLUSTERED " : "NONCLUSTERED ");

                sb.Append($"INDEX [{indexName}] ON ");
                sb.Append($"[{recommendation.DatabaseName}].[{recommendation.SchemaName}].[{recommendation.TableName}] (");

                // Add key columns
                for (int i = 0; i < recommendation.KeyColumns.Count; i++)
                {
                    var column = recommendation.KeyColumns[i];

                    if (i > 0)
                        sb.Append(", ");

                    sb.Append($"[{column.ColumnName}]");

                    if (column.SortDirection == SortDirection.Descending)
                        sb.Append(" DESC");
                    else
                        sb.Append(" ASC");
                }

                sb.Append(")");

                // Add included columns if any
                if (recommendation.IncludedColumns.Count > 0)
                {
                    sb.Append(" INCLUDE (");

                    for (int i = 0; i < recommendation.IncludedColumns.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");

                        sb.Append($"[{recommendation.IncludedColumns[i].ColumnName}]");
                    }

                    sb.Append(")");
                }

                // Add filter if any
                if (!string.IsNullOrWhiteSpace(recommendation.FilterCondition))
                {
                    sb.Append($" WHERE {recommendation.FilterCondition}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CREATE INDEX statement");
                return string.Empty;
            }
        }

        private List<string> ExtractTableNamesFromQuery(string query)
        {
            var tableNames = new List<string>();

            if (string.IsNullOrWhiteSpace(query))
                return tableNames;

            try
            {
                // This is a simplistic approach to extracting table names
                // In a real implementation, we would use a SQL parser

                // Match tables after FROM or JOIN
                var tablePattern = @"(?:FROM|JOIN)\s+(?:\[([^\]]+)\]|([a-zA-Z0-9_]+))";
                var matches = Regex.Matches(query, tablePattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string tableName = match.Groups[1].Success
                        ? match.Groups[1].Value
                        : match.Groups[2].Value;

                    if (!string.IsNullOrWhiteSpace(tableName) && !tableNames.Contains(tableName))
                    {
                        tableNames.Add(tableName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting table names from query");
            }

            return tableNames;
        }

        private async Task<List<RedundantIndexInfo>> FindRedundantIndexesForTableAsync(
            SqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            var redundantIndexes = new List<RedundantIndexInfo>();

            try
            {
                using (var command = connection.CreateCommand())
                {
                    // Query to find potentially redundant indexes
                    command.CommandText = @"
                        WITH IndexColumns AS (
                            SELECT 
                                i.object_id,
                                i.index_id,
                                i.name AS index_name,
                                i.type_desc AS index_type,
                                i.is_unique,
                                i.is_primary_key,
                                i.filter_definition,
                                OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
                                OBJECT_NAME(i.object_id) AS table_name,
                                c.name AS column_name,
                                ic.is_included_column,
                                ic.key_ordinal,
                                ic.is_descending_key
                            FROM sys.indexes i
                            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                            WHERE OBJECT_NAME(i.object_id) = @tableName
                            AND i.is_hypothetical = 0
                            AND i.index_id > 0 -- Exclude heaps
                        ),
                        DuplicateIndexes AS (
                            SELECT 
                                a.schema_name,
                                a.table_name,
                                a.index_name AS index_name_1,
                                b.index_name AS index_name_2,
                                a.index_type AS index_type_1,
                                b.index_type AS index_type_2,
                                a.is_unique AS is_unique_1,
                                b.is_unique AS is_unique_2
                            FROM IndexColumns a
                            JOIN IndexColumns b ON a.object_id = b.object_id
                                AND a.index_id < b.index_id -- Only compare in one direction
                                AND a.column_name = b.column_name
                                AND a.key_ordinal = b.key_ordinal
                                AND a.is_included_column = b.is_included_column
                                AND a.is_descending_key = b.is_descending_key
                            GROUP BY 
                                a.schema_name, a.table_name, a.index_name, b.index_name,
                                a.index_type, b.index_type, a.is_unique, b.is_unique
                            HAVING COUNT(*) > 0 -- At least one matching column
                        )
                        SELECT
                            schema_name,
                            table_name,
                            index_name_2 AS index_name,
                            'Potentially redundant with ' + index_name_1 AS reason,
                            'Consider combining or removing. Validate actual usage first.' AS recommendation,
                            'DROP INDEX [' + index_name_2 + '] ON [' + schema_name + '].[' + table_name + ']' AS drop_statement
                        FROM DuplicateIndexes";

                    command.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 128) { Value = tableName });

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var info = new RedundantIndexInfo
                            {
                                SchemaName = reader["schema_name"].ToString(),
                                TableName = reader["table_name"].ToString(),
                                IndexName = reader["index_name"].ToString(),
                                Reason = reader["reason"].ToString(),
                                Recommendation = reader["recommendation"].ToString(),
                                DropIndexStatement = reader["drop_statement"].ToString()
                            };

                            redundantIndexes.Add(info);
                        }
                    }
                }

                // Get index usage stats
                if (redundantIndexes.Count > 0)
                {
                    await EnrichIndexUsageInfoAsync(connection, redundantIndexes, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding redundant indexes for table {TableName}", tableName);
            }

            return redundantIndexes;
        }

        private async Task EnrichIndexUsageInfoAsync(
            SqlConnection connection,
            List<RedundantIndexInfo> indexes,
            CancellationToken cancellationToken)
        {
            try
            {
                // Prepare placeholders for the IN clause
                var tableParameters = new List<string>();
                var indexParameters = new List<string>();

                for (int i = 0; i < indexes.Count; i++)
                {
                    tableParameters.Add($"@table{i}");
                    indexParameters.Add($"@index{i}");
                }

                using (var command = connection.CreateCommand())
                {
                    // Query to get index usage statistics
                    command.CommandText = $@"
                        SELECT 
                            OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
                            OBJECT_NAME(i.object_id) AS table_name,
                            i.name AS index_name,
                            ISNULL(s.user_seeks, 0) + ISNULL(s.user_scans, 0) + ISNULL(s.user_lookups, 0) AS usage_count,
                            ISNULL(s.last_user_seek, ISNULL(s.last_user_scan, s.last_user_lookup)) AS last_used_date,
                            p.used_page_count * 8 / 1024.0 AS size_mb
                        FROM sys.indexes i
                        LEFT JOIN sys.dm_db_index_usage_stats s ON 
                            i.object_id = s.object_id AND i.index_id = s.index_id AND s.database_id = DB_ID()
                        LEFT JOIN sys.dm_db_partition_stats p ON 
                            i.object_id = p.object_id AND i.index_id = p.index_id
                        WHERE OBJECT_NAME(i.object_id) IN ({string.Join(", ", tableParameters)})
                        AND i.name IN ({string.Join(", ", indexParameters)})";

                    // Add parameters
                    for (int i = 0; i < indexes.Count; i++)
                    {
                        command.Parameters.Add(new SqlParameter(tableParameters[i], SqlDbType.NVarChar, 128) { Value = indexes[i].TableName });
                        command.Parameters.Add(new SqlParameter(indexParameters[i], SqlDbType.NVarChar, 128) { Value = indexes[i].IndexName });
                    }

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schemaName = reader["schema_name"].ToString();
                            string tableName = reader["table_name"].ToString();
                            string indexName = reader["index_name"].ToString();

                            // Find the matching index
                            var index = indexes.Find(i =>
                                i.SchemaName == schemaName &&
                                i.TableName == tableName &&
                                i.IndexName == indexName);

                            if (index != null)
                            {
                                index.UsageCount = Convert.ToInt64(reader["usage_count"]);
                                index.SizeMB = Convert.ToDouble(reader["size_mb"]);

                                if (!reader.IsDBNull(reader.GetOrdinal("last_used_date")))
                                {
                                    index.LastUsedDate = Convert.ToDateTime(reader["last_used_date"]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching index usage information");
            }
        }
    }
}