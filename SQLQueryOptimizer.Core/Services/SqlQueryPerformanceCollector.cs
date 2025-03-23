using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
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
    /// Implementation of IQueryPerformanceCollector for SQL Server
    /// </summary>
    public class SqlQueryPerformanceCollector : IQueryPerformanceCollector
    {
        private readonly ILogger<SqlQueryPerformanceCollector> _logger;

        /// <summary>
        /// Initializes a new instance of the SqlQueryPerformanceCollector
        /// </summary>
        public SqlQueryPerformanceCollector(ILogger<SqlQueryPerformanceCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<QueryPerformanceMetrics> CollectMetricsAsync(
            string query,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _logger.LogInformation("Collecting performance metrics for SQL query");

            var metrics = new QueryPerformanceMetrics();

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    // Get execution plan first
                    metrics.ExecutionPlanXml = await GetExecutionPlanAsync(query, connectionString, true, cancellationToken);

                    // Get estimated plan cost from the execution plan
                    metrics.EstimatedPlanCost = ExtractEstimatedCostFromPlan(metrics.ExecutionPlanXml);

                    // Enable statistics for the connection
                    using (var statCommand = connection.CreateCommand())
                    {
                        statCommand.CommandText = "SET STATISTICS IO ON; SET STATISTICS TIME ON;";
                        await statCommand.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = ReplaceParametersWithDummyValues(query);
                        command.CommandTimeout = 30; // 30 seconds timeout

                        var stopwatch = Stopwatch.StartNew();

                        // Execute the query and process the results
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                        {
                            long rowCount = 0;

                            // Process all rows to ensure full execution
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                rowCount++;
                            }

                            metrics.RowsReturned = rowCount;
                        }

                        stopwatch.Stop();
                        metrics.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                    }

                    // Get additional performance information using system DMVs
                    await CollectAdditionalMetricsAsync(connection, query, metrics, cancellationToken);
                }

                // Mask sensitive data in connection string
                metrics.MaskedConnectionString = MaskConnectionString(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting performance metrics");
                throw;
            }

            return metrics;
        }

        /// <inheritdoc />
        public async Task<QueryPerformanceMetrics> BenchmarkQueryAsync(
            string query,
            string connectionString,
            int iterations = 3,
            int warmupIterations = 1,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (iterations <= 0)
                throw new ArgumentException("Iterations must be greater than 0", nameof(iterations));

            _logger.LogInformation("Benchmarking SQL query with {Iterations} iterations and {WarmupIterations} warmup iterations",
                iterations, warmupIterations);

            // Perform warmup iterations
            for (int i = 0; i < warmupIterations; i++)
            {
                await ExecuteQueryAsync(query, connectionString, cancellationToken);
            }

            // Collect metrics for each iteration
            var metricsList = new List<QueryPerformanceMetrics>();

            for (int i = 0; i < iterations; i++)
            {
                var metrics = await CollectMetricsAsync(query, connectionString, cancellationToken);
                metricsList.Add(metrics);
            }

            // Aggregate metrics
            return AggregateMetrics(metricsList);
        }

        /// <inheritdoc />
        public async Task<string> GetExecutionPlanAsync(
            string query,
            string connectionString,
            bool actualExecutionPlan = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            try
            {
                // Replace parameters with dummy values for execution plan
                string processedQuery = ReplaceParametersWithDummyValues(query);

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    using (var command = connection.CreateCommand())
                    {
                        // Turn on execution plan
                        if (actualExecutionPlan)
                        {
                            command.CommandText = "SET STATISTICS XML ON;";
                        }
                        else
                        {
                            command.CommandText = "SET SHOWPLAN_XML ON;";
                        }

                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }

                    string planXml = null;

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = processedQuery;
                        command.CommandTimeout = 30; // 30 seconds timeout

                        if (actualExecutionPlan)
                        {
                            // For actual execution plan, we need to execute the query
                            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                            {
                                // Read all result sets
                                do
                                {
                                    while (await reader.ReadAsync(cancellationToken))
                                    {
                                        // Process rows if needed
                                    }
                                } while (await reader.NextResultAsync(cancellationToken));

                                // The execution plan is returned as a message
                                if (reader.GetSchemaTable() != null)
                                {
                                    while (await reader.ReadAsync(cancellationToken))
                                    {
                                        if (reader.FieldCount > 0 && reader[0] is string)
                                        {
                                            planXml = reader[0].ToString();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // For estimated plan, we don't execute the query
                            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                            {
                                if (await reader.ReadAsync(cancellationToken) && reader.FieldCount > 0)
                                {
                                    planXml = reader[0].ToString();
                                }
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        // Turn off execution plan
                        if (actualExecutionPlan)
                        {
                            command.CommandText = "SET STATISTICS XML OFF;";
                        }
                        else
                        {
                            command.CommandText = "SET SHOWPLAN_XML OFF;";
                        }

                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }

                    return planXml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting execution plan");
                return null;
            }
        }

        // Add this helper method to replace parameters with dummy values
        private string ReplaceParametersWithDummyValues(string query)
        {
            // Replace @parameter with string '1' for string parameters
            var stringParamRegex = new System.Text.RegularExpressions.Regex(@"LIKE\s+@\w+");
            query = stringParamRegex.Replace(query, "LIKE '1'");

            // Replace general parameters with numeric 1
            var paramRegex = new System.Text.RegularExpressions.Regex(@"@\w+");
            query = paramRegex.Replace(query, "1");

            return query;
        }

        private async Task ExecuteQueryAsync(string query, string connectionString, CancellationToken cancellationToken)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = ReplaceParametersWithDummyValues(query);
                        command.CommandTimeout = 30; // 30 seconds timeout

                        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                        {
                            // Process all rows to ensure full execution
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                // Just read, don't process
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query during warmup");
            }
        }

        private async Task CollectAdditionalMetricsAsync(
            SqlConnection connection,
            string query,
            QueryPerformanceMetrics metrics,
            CancellationToken cancellationToken)
        {
            try
            {
                // Use sys.dm_exec_query_stats DMV to get additional metrics
                // This is simplified and would be much more complex in a real implementation

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT TOP 1
                            qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
                            qs.total_physical_reads / qs.execution_count AS avg_physical_reads,
                            qs.total_worker_time / qs.execution_count AS avg_cpu_time_ms,
                            qs.total_rows / qs.execution_count AS avg_rows,
                            qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time_ms,
                            qs.execution_count,
                            qs.plan_generation_num
                        FROM sys.dm_exec_query_stats AS qs
                        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
                        WHERE st.text LIKE @query
                        ORDER BY qs.last_execution_time DESC";

                    // Add parameter to search for the query text
                    var queryParam = command.Parameters.Add("@query", SqlDbType.NVarChar, -1);
                    queryParam.Value = "%" + query.Replace("'", "''") + "%";

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            metrics.LogicalReads = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader[0]);
                            metrics.PhysicalReads = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader[1]);
                            metrics.CpuTimeMs = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader[2]);
                            metrics.RowsScanned = reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader[3]);
                            metrics.PlanReuseCount = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]);
                            metrics.IsPlanCached = metrics.PlanReuseCount > 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting additional metrics from DMVs");
            }
        }

        private double ExtractEstimatedCostFromPlan(string planXml)
        {
            if (string.IsNullOrWhiteSpace(planXml))
                return 0;

            try
            {
                // In a real implementation, we would use XML parsing to extract the cost
                // This is a simplified placeholder
                const string costAttribute = "EstimatedTotalSubtreeCost";
                int costIndex = planXml.IndexOf(costAttribute);

                if (costIndex < 0)
                    return 0;

                int valueStart = planXml.IndexOf("\"", costIndex) + 1;
                int valueEnd = planXml.IndexOf("\"", valueStart);

                if (valueStart < 0 || valueEnd < 0)
                    return 0;

                string costValue = planXml.Substring(valueStart, valueEnd - valueStart);

                if (double.TryParse(costValue, out double cost))
                    return cost;

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting estimated cost from execution plan");
                return 0;
            }
        }

        private QueryPerformanceMetrics AggregateMetrics(List<QueryPerformanceMetrics> metricsList)
        {
            if (metricsList == null || metricsList.Count == 0)
                return new QueryPerformanceMetrics();

            var result = new QueryPerformanceMetrics
            {
                ExecutionPlanXml = metricsList[0].ExecutionPlanXml,
                MaskedConnectionString = metricsList[0].MaskedConnectionString,
                EstimatedPlanCost = metricsList[0].EstimatedPlanCost,
                IsPlanCached = metricsList[0].IsPlanCached,
                PlanReuseCount = metricsList[0].PlanReuseCount
            };

            // Calculate averages
            foreach (var metrics in metricsList)
            {
                result.ExecutionTimeMs += metrics.ExecutionTimeMs;
                result.CpuTimeMs += metrics.CpuTimeMs;
                result.LogicalReads += metrics.LogicalReads;
                result.PhysicalReads += metrics.PhysicalReads;
                result.RowsReturned += metrics.RowsReturned;
                result.RowsScanned += metrics.RowsScanned;
            }

            int count = metricsList.Count;
            result.ExecutionTimeMs /= count;
            result.CpuTimeMs /= count;
            result.LogicalReads /= count;
            result.PhysicalReads /= count;
            result.RowsReturned /= count;
            result.RowsScanned /= count;

            return result;
        }

        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                // Mask password
                if (!string.IsNullOrEmpty(builder.Password))
                {
                    builder.Password = "********";
                }

                return builder.ConnectionString;
            }
            catch (Exception)
            {
                // If we can't parse the connection string, return a more heavily masked version
                return Regex.Replace(connectionString,
                    @"(Password|PWD)=([^;]*)",
                    "$1=********",
                    RegexOptions.IgnoreCase);
            }
        }
    }
}