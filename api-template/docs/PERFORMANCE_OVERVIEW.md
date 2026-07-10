# Performance Overview

The API template is designed for high throughput and low latency, utilizing multi-tiered caching and optimized data access patterns.

## Performance Pillars

### 1. Response Caching
We use **Output Caching** to serve frequently requested, non-sensitive data (like public place lists or directories) directly from memory, bypassing the entire application logic and database.
*   **See:** [Caching Strategy](performance/CACHING_STRATEGY.md)

### 2. Database Optimization
Our persistence layer uses **EF Core Advanced Performance** techniques to minimize overhead:
*   **DbContext Pooling:** Reduces the cost of creating database contexts.
*   **Compiled Queries:** Bypasses LINQ expression tree hashing for high-frequency read paths.
*   **AsNoTracking:** Minimizes memory allocation for read-only requests.
*   **See:** [EF Core Performance Enforcements](performance/EFCORE_ADVANCED_PERFORMANCE_TOPICS.md)

### 3. Spatial Efficiency
Proximity searches are performed using **PostGIS GIST indexing** on `geography` types, ensuring that "nearby" queries remain fast even as row counts grow.
*   **See:** [Spatial Queries & PostGIS](features/SPATIAL_QUERIES.md)
