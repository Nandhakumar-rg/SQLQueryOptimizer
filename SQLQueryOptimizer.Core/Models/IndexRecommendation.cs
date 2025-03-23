using System;
using System.Collections.Generic;

namespace SQLQueryOptimizer.Core.Models
{
    /// <summary>
    /// Represents an index recommendation for improving query performance
    /// </summary>
    public class IndexRecommendation
    {
        /// <summary>
        /// Database name where the index should be created
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Schema name of the table
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Table name that should be indexed
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// List of columns that should be included in the index key
        /// </summary>
        public List<IndexColumn> KeyColumns { get; set; } = new List<IndexColumn>();

        /// <summary>
        /// List of columns that should be included in the index but not as key columns
        /// </summary>
        public List<IndexColumn> IncludedColumns { get; set; } = new List<IndexColumn>();

        /// <summary>
        /// Type of index to create
        /// </summary>
        public IndexType IndexType { get; set; }

        /// <summary>
        /// Whether the index should be unique
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// DDL statement to create the recommended index
        /// </summary>
        public string CreateIndexStatement { get; set; }

        /// <summary>
        /// Estimated impact of the index on the query performance (percentage improvement)
        /// </summary>
        public double EstimatedImpact { get; set; }

        /// <summary>
        /// Filter condition for filtered indexes
        /// </summary>
        public string FilterCondition { get; set; }

        /// <summary>
        /// Generate a suggested index name based on table and columns
        /// </summary>
        /// <returns>A suggested name for the index</returns>
        public string SuggestIndexName()
        {
            string columnPrefix = string.Join("_", KeyColumns.ConvertAll(c => c.ColumnName.Substring(0, Math.Min(3, c.ColumnName.Length))));
            string indexTypePrefix = IndexType == IndexType.Clustered ? "CIX" : "IX";
            return $"{indexTypePrefix}_{TableName}_{columnPrefix}";
        }
    }

    /// <summary>
    /// Represents a column in an index recommendation
    /// </summary>
    public class IndexColumn
    {
        /// <summary>
        /// Name of the column
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Sort direction for the column (Ascending or Descending)
        /// </summary>
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

        /// <summary>
        /// Data type of the column
        /// </summary>
        public string DataType { get; set; }
    }

    /// <summary>
    /// Type of index
    /// </summary>
    public enum IndexType
    {
        Nonclustered,
        Clustered,
        ColumnStore,
        Spatial,
        XML
    }

    /// <summary>
    /// Sort direction for index columns
    /// </summary>
    public enum SortDirection
    {
        Ascending,
        Descending
    }
}