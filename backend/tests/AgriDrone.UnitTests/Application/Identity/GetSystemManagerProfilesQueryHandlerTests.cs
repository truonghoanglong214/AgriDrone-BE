using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfile;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfiles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application.Pagination;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class GetSystemManagerProfilesQueryHandlerTests
{
    private static readonly SystemManagerProfileResponse Profile = new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        "manager@example.com",
        "System Manager",
        SystemManagerProfileStatus.Suspended,
        SystemManagerAvailabilityStatus.Unavailable,
        FlightQualificationStatus.Pending,
        QualificationExpiresAt: null,
        Version: 1);

    [Fact]
    public async Task GetByIdReturnsProfileWithCurrentVersion()
    {
        var queries = new FakeSystemManagerProfileQueries(Profile);
        var handler = new GetSystemManagerProfileQueryHandler(queries);

        var result = await handler.Handle(
            new GetSystemManagerProfileQuery(Profile.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Profile.Id, result.Value.Id);
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task GetByIdReturnsNotFoundWhenProfileDoesNotExist()
    {
        var handler = new GetSystemManagerProfileQueryHandler(
            new FakeSystemManagerProfileQueries(profile: null));

        var result = await handler.Handle(
            new GetSystemManagerProfileQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SystemManager.ProfileNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetPageForwardsFiltersAndPagination()
    {
        var queries = new FakeSystemManagerProfileQueries(Profile);
        var handler = new GetSystemManagerProfilesQueryHandler(queries);
        var query = new GetSystemManagerProfilesQuery(
            SystemManagerProfileStatus.Suspended,
            SystemManagerAvailabilityStatus.Unavailable,
            FlightQualificationStatus.Pending,
            PageNumber: 2,
            PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(query.Status, queries.Status);
        Assert.Equal(query.Availability, queries.Availability);
        Assert.Equal(query.QualificationStatus, queries.QualificationStatus);
        Assert.Equal(2, queries.PageRequest!.PageNumber);
        Assert.Equal(10, queries.PageRequest.PageSize);
    }

    private sealed class FakeSystemManagerProfileQueries(
        SystemManagerProfileResponse? profile)
        : ISystemManagerProfileQueries
    {
        public SystemManagerProfileStatus? Status { get; private set; }
        public SystemManagerAvailabilityStatus? Availability { get; private set; }
        public FlightQualificationStatus? QualificationStatus { get; private set; }
        public PagedRequest? PageRequest { get; private set; }

        public Task<PagedResult<SystemManagerProfileResponse>> GetPageAsync(
            SystemManagerProfileStatus? status,
            SystemManagerAvailabilityStatus? availability,
            FlightQualificationStatus? qualificationStatus,
            PagedRequest pageRequest,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            Availability = availability;
            QualificationStatus = qualificationStatus;
            PageRequest = pageRequest;
            IReadOnlyList<SystemManagerProfileResponse> items =
                profile is null ? [] : [profile];
            return Task.FromResult(
                new PagedResult<SystemManagerProfileResponse>(
                    items,
                    pageRequest.PageNumber,
                    pageRequest.PageSize,
                    items.Count));
        }

        public Task<SystemManagerProfileResponse?> GetByIdAsync(
            Guid profileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                profile?.Id == profileId ? profile : null);
    }
}
