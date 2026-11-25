using System.Security.Claims;
using AgriDrone.Api.Controllers;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Application.Features.LogoutSession;
using AgriDrone.Modules.Identity.Application.Features.RefreshSession;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class AuthCookieEndpointTests
{
    [Fact]
    public async Task RefreshReturns403WithoutSessionHeaderOrAllowedOriginBeforeCallingMediator()
    {
        var sender = new RecordingSender();
        var controller = CreateController(sender);
        controller.HttpContext.Request.Headers.Origin = "https://evil.example";

        var result = await controller.Refresh(CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    public async Task RefreshReturns409AndDoesNotOverwriteCookieWhenConflictOccurs()
    {
        var sender = new RecordingSender
        {
            NextResult = Result.Failure<AuthenticationCommandResult>(
                AppError.Conflict("Authentication.RefreshConflict", "Concurrent rotation."))
        };
        var controller = CreateController(sender);
        controller.HttpContext.Request.Headers[AuthController.SessionHeaderName] = "1";
        controller.HttpContext.Request.Headers.Origin = "https://app.agridrone.vn";
        controller.HttpContext.Request.Headers.Cookie = "agridrone_refresh=dummy-token";

        var result = await controller.Refresh(CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.False(controller.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task RefreshSetsCookieAndNoStoreHeaderWhenSucceeded()
    {
        var t0 = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var payload = new LoginUserResponse(
            "owner@example.com",
            "Owner",
            null,
            new AuthenticationSessionResponse("jwt-access", t0.AddMinutes(15), null),
            null);
        var sender = new RecordingSender
        {
            NextResult = Result.Success(new AuthenticationCommandResult(
                payload,
                new IssuedRefreshCookie("rotated-raw-cookie", t0.AddDays(7))))
        };
        var controller = CreateController(sender);
        controller.HttpContext.Request.IsHttps = true;
        controller.HttpContext.Request.Headers[AuthController.SessionHeaderName] = "1";
        controller.HttpContext.Request.Headers.Origin = "https://app.agridrone.vn";
        controller.HttpContext.Request.Headers.Cookie = "agridrone_refresh=old-raw-cookie";

        var result = await controller.Refresh(CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<LoginUserResponse>>(result);
        Assert.Equal(StatusCodes.Status200OK, json.StatusCode);
        Assert.Equal("no-store", controller.HttpContext.Response.Headers.CacheControl.ToString());
        var setCookie = controller.HttpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains("agridrone_refresh=rotated-raw-cookie", setCookie);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutDeletesCookieAndReturns204EvenWithoutCookie()
    {
        var sender = new RecordingSender();
        var controller = CreateController(sender);
        controller.HttpContext.Request.IsHttps = true;
        controller.HttpContext.Request.Headers[AuthController.SessionHeaderName] = "1";
        controller.HttpContext.Request.Headers.Origin = "https://app.agridrone.vn";

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<NoContent>(result);
        var setCookie = controller.HttpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains("agridrone_refresh=", setCookie);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateController(ISender sender)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RefreshSession:AllowedOrigins:0"] = "https://app.agridrone.vn"
            })
            .Build();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider()
        };

        return new AuthController(
            sender,
            Options.Create(new RefreshSessionOptions
            {
                AllowedOrigins = ["https://app.agridrone.vn"]
            }),
            config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private sealed class RecordingSender : ISender
    {
        public int Calls { get; private set; }
        public Result<AuthenticationCommandResult>? NextResult { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (request is RefreshSessionCommand && NextResult is TResponse typed)
                return Task.FromResult(typed);
            if (request is LogoutSessionCommand && Result.Success() is TResponse logoutResult)
                return Task.FromResult(logoutResult);
            throw new InvalidOperationException($"Unexpected request {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
