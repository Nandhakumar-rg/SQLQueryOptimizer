using System.Linq;
using SQLQueryOptimizer.Core.Analyzers;
using SQLQueryOptimizer.Core.Models;
using Xunit;

namespace SQLQueryOptimizer.Tests
{
    public class QueryPatternAnalyzerTests
    {
        [Fact]
        public void AnalyzeQuery_DetectsSelectStar()
        {
            // Arrange
            var analyzer = new QueryPatternAnalyzer();
            var query = "SELECT * FROM Customers";

            // Act
            var issues = analyzer.AnalyzeQuery(query);

            // Assert
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.ColumnWildcard);
        }

        [Fact]
        public void AnalyzeQuery_DetectsNoLock()
        {
            // Arrange
            var analyzer = new QueryPatternAnalyzer();
            var query = "SELECT Id, Name FROM Customers WITH (NOLOCK)";

            // Act
            var issues = analyzer.AnalyzeQuery(query);

            // Assert
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.TableScan);
        }

        [Fact]
        public void AnalyzeQuery_DetectsLeadingWildcard()
        {
            // Arrange
            var analyzer = new QueryPatternAnalyzer();
            var query = "SELECT Id, Name FROM Customers WHERE Name LIKE '%Smith'";

            // Act
            var issues = analyzer.AnalyzeQuery(query);

            // Assert
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.NonSargableCondition);
        }

        [Fact]
        public void AnalyzeQuery_MultipleFindingsForComplexQuery()
        {
            // Arrange
            var analyzer = new QueryPatternAnalyzer();
            var query = @"
                SELECT DISTINCT * 
                FROM Customers c, Orders o
                WHERE UPPER(c.LastName) = 'SMITH'
                AND o.CustomerId = c.Id
                AND c.FirstName LIKE '%John%'";

            // Act
            var issues = analyzer.AnalyzeQuery(query);

            // Assert
            Assert.True(issues.Count >= 3, "Should detect at least 3 issues");
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.ColumnWildcard);
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.NonSargableCondition);
            Assert.Contains(issues, i => i.IssueType == QueryIssueType.CartesianProduct);
        }
    }
}