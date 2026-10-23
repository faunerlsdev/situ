namespace Situ.Application.Features.Identity.RegisterUser;

public sealed record RegisterUserRequest(
    string Email,
    string UserName,
    string Password,
    DateOnly DateOfBirth,
    Guid CountryId,
    Guid RegionId,
    Guid CityId,
    IReadOnlyList<Guid> AcceptedDocumentIds,
    string? IpAddress,
    string? UserAgent);
