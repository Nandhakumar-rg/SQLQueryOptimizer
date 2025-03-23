using System;
using System.Collections.Generic;

namespace SQLQueryOptimizer.Core.Models
{
    /// <summary>
    /// Represents performance metrics for a SQL query execution
    /// </summary>
    public class QueryPerformanceMetrics
    {
        /// <summary>
        /// Total execution time in milliseconds
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// CPU time used in milliseconds
        /// </summary>
        public double CpuTimeMs { get; set; }

        /// <summary>
        /// Logical reads (pages read from buffer cache)
        /// </summary>
        public long LogicalReads { get; set; }

        /// <summary>
        /// Physical reads (pages read from disk)
        /// </summary>
        public long PhysicalReads { get; set; }

        /// <summary>
        /// Number of rows returned by the query
        /// </summary>
        public long RowsReturned { get; set; }

        /// <summary>
        /// Number of rows processed/scanned by the query
        /// </summary>
        public long RowsScanned { get; set; }

        /// <summary>
        /// Query execution plan in XML format (if available)
        /// </summary>
        public string ExecutionPlanXml { get; set; }

        /// <summary>
        /// Whether the execution plan was cached
        /// </summary>
        public bool IsPlanCached { get; set; }

        /// <summary>
        /// Cost of the execution plan (as estimated by the query optimizer)
        /// </summary>
        public double EstimatedPlanCost { get; set; }

        /// <summary>
        /// Connection string used (with sensitive data masked)
        /// </summary>
        public string MaskedConnectionString { get; set; }

        /// <summary>
        /// Number of query compilation reuses
        /// </summary>
        public int PlanReuseCount { get; set; }
    }
}