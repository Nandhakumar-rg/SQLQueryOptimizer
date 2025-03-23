# Advanced SQL Query Optimizer for .NET

A powerful library for optimizing SQL queries in .NET applications. This package helps developers identify and fix performance issues in their SQL queries.

## Features

- **Query Analysis**: Automatically detects common SQL anti-patterns and performance issues
- **Optimization Suggestions**: Provides detailed suggestions to improve query performance
- **Index Recommendations**: Analyzes execution plans to suggest missing indexes
- **Performance Benchmarking**: Measures and compares query performance
- **Integration with Popular ORMs**: Works with Entity Framework Core and Dapper

## Getting Started

### Installation

```bash
# Install the core package
dotnet add package SQLQueryOptimizer.Core

# For Entity Framework Core integration
dotnet add package SQLQueryOptimizer.EntityFramework

# For Dapper integration
dotnet add package SQLQueryOptimizer.Dapper
```

### Basic Usage


// Register services in DI container
services.AddSqlQueryOptimizer();

// Analyze a SQL query
var result = await queryAnalyzer.AnalyzeQueryAsync(
    "SELECT * FROM Products WHERE CategoryId = 1",
    connectionString);

// View optimization suggestions
foreach (var suggestion in result.Suggestions)
{
    Console.WriteLine($"{suggestion.Title}: {suggestion.Description}");
}

// View index recommendations
foreach (var index in result.IndexRecommendations)
{
    Console.WriteLine($"Create index on {index.TableName}: {index.CreateIndexStatement}");
}

// Benchmark a query
var metrics = await queryAnalyzer.BenchmarkQueryAsync(
    "SELECT * FROM Products WHERE CategoryId = 1",
    connectionString,
    iterations: 5);

Console.WriteLine($"Average execution time: {metrics.ExecutionTimeMs} ms");


## Entity Framework Core Integration


// Analyze an EF Core query
var query = dbContext.Products.Where(p => p.CategoryId == 1);
var result = await query.AnalyzeAsync(queryAnalyzer);


## Dapper Integration

// Analyze a Dapper query
var sql = "SELECT * FROM Products WHERE CategoryId = @categoryId";
var result = await connection.AnalyzeQueryAsync(sql, queryAnalyzer);


## License

This project is licensed under the MIT License - see the LICENSE file for details.




