using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfile;

internal sealed class GetSystemManagerProfileQueryHandler(
    ISystemManagerProfileQueries queries)
    : IRequestHandler<
        GetSystemManagerProfileQuery,
        Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        GetSystemManagerProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await queries.GetByIdAsync(
            request.ProfileId,
            cancellationToken);

        return profile is null
            ? Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ProfileNotFound())
            : Result.Success(profile);
    }
}
