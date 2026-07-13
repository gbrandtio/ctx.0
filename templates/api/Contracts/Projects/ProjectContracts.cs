namespace Contracts.Projects;

public sealed record CreateProjectRequest(string Name);

public sealed record ProjectResponse(long Id, long OrgId, string Name, DateTime CreatedAt);
