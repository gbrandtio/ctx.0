# ADR 0001: Domain Layer Persistence Awareness

## Status
Accepted

## Context
The project follows Clean Architecture principles, which typically mandate that the Domain layer should be "persistence-ignorant" and have no dependencies on infrastructure frameworks like Entity Framework Core. However, the project has approximately 30+ entities. Duplicating these entities into separate "Persistence Models" (for EF Core mapping) and "Domain Models" (for business logic) would significantly increase complexity and maintenance overhead.

## Decision
We will use the same classes as both Domain Entities and EF Core Persistence Models. This means the `Domain` project will have a direct dependency on `Microsoft.EntityFrameworkCore`.

## Consequences
- **Positive:** Reduced code duplication and maintenance overhead. Simplified mapping logic.
- **Negative:** The Domain layer is now technically coupled to EF Core.
- **Constraint:** Business logic within entities must still remain focused on domain rules. EF Core attributes and configurations should be kept to the minimum necessary for mapping.
- **Rule:** This is an explicit exception to the project's Clean Architecture standards.
