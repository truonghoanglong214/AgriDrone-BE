using System.Net;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MetashapeProcessingTests
{
    private static readonly double[] SampleCorner = [10.1299, 106.1234];

    [Fact]
    public async Task CreateTaskAsyncSubmitsPayloadAndReturnsTaskId()
    {
        var expectedTaskId = Guid.NewGuid().ToString("D");
        var responseJson = JsonSerializer.Serialize(new
        {
            task_id = expectedTaskId,
            status = "QUEUED",
            message = "Metashape task submitted"
        });

        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/api/v1/metashape/process", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var missionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var images = new List<MetashapeTaskImageItem>
        {
            new("tenants/test/media/img1.jpg", "http://minio/img1.jpg")
        };

        var result = await service.CreateTaskAsync(missionId, tenantId, farmId, images);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTaskId, result.TaskId);
    }

    [Fact]
    public async Task CreateTaskAsyncWhenEngineIsOdmSubmitsToOdmEndpoint()
    {
        var expectedTaskId = Guid.NewGuid().ToString("D");
        var responseJson = JsonSerializer.Serialize(new
        {
            task_id = expectedTaskId,
            status = "QUEUED",
            message = "OpenDroneMap task submitted"
        });

        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/api/v1/odm/process", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var missionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var images = new List<MetashapeTaskImageItem>
        {
            new("tenants/test/media/img1.jpg", "http://minio/img1.jpg")
        };

        var result = await service.CreateTaskAsync(missionId, tenantId, farmId, images, null, "odm");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTaskId, result.TaskId);
    }

    [Fact]
    public async Task CreateTaskAsyncWhenNoImagesReturnsFailure()
    {
        using var client = new HttpClient();
        var options = Options.Create(new MetashapeOptions());
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var result = await service.CreateTaskAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), []);

        Assert.False(result.IsSuccess);
        Assert.Contains("No image items provided", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateTaskAsyncWhenHttpErrorReturnsFailure()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server crashed", System.Text.Encoding.UTF8, "text/plain")
            }));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var images = new List<MetashapeTaskImageItem> { new("key1", null) };
        var result = await service.CreateTaskAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), images);

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task GetTaskStatusAsyncWhenCompletedParsesMetadataCorrectly()
    {
        var taskId = Guid.NewGuid().ToString("D");
        var responseJson = JsonSerializer.Serialize(new
        {
            task_id = taskId,
            status = "COMPLETED",
            progress = 100,
            stage = "Completed",
            processing_time_ms = 45000,
            tif_storage_key = "tenants/test/orthophoto.tif",
            jpg_storage_key = "tenants/test/map_preview.jpg",
            tif_url = "http://minio/orthophoto.tif",
            preview_url = "http://minio/map_preview.jpg",
            tif_size_bytes = 500000000,
            jpg_size_bytes = 15000000,
            tif_checksum = "abc123tif",
            jpg_checksum = "xyz789jpg",
            original_width = 8000,
            original_height = 6000,
            bounds = new
            {
                min_lat = 10.1234,
                max_lat = 10.1299,
                min_lon = 106.1234,
                max_lon = 106.1299
            },
            corners = new
            {
                north_west = SampleCorner,
                north_east = SampleCorner,
                south_east = SampleCorner,
                south_west = SampleCorner
            }
        });

        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"/api/v1/metashape/tasks/{taskId}", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var result = await service.GetTaskStatusAsync(taskId);

        Assert.True(result.IsSuccess);
        Assert.Equal("COMPLETED", result.StatusName);
        Assert.Equal(40, result.StatusCode);
        Assert.Equal(100, result.Progress);
        Assert.NotNull(result.Output);
        Assert.Equal("tenants/test/orthophoto.tif", result.Output.TifStorageKey);
        Assert.Equal("tenants/test/map_preview.jpg", result.Output.JpgStorageKey);
        Assert.Equal(8000, result.Output.OriginalWidth);
        Assert.Equal(6000, result.Output.OriginalHeight);
        Assert.NotNull(result.Output.Bounds);
        Assert.Equal(10.1234, result.Output.Bounds.MinLat);
        Assert.NotNull(result.Output.Corners);
        Assert.Equal(10.1299, result.Output.Corners.NorthWest.Latitude);
    }

    [Fact]
    public async Task GetTaskStatusAsyncWhenRunningParsesProgress()
    {
        var taskId = Guid.NewGuid().ToString("D");
        var responseJson = JsonSerializer.Serialize(new
        {
            task_id = taskId,
            status = "RUNNING",
            progress = 65,
            stage = "Building 3D Mesh Model",
            processing_time_ms = 25000
        });

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var result = await service.GetTaskStatusAsync(taskId);

        Assert.True(result.IsSuccess);
        Assert.Equal("RUNNING", result.StatusName);
        Assert.Equal(20, result.StatusCode);
        Assert.Equal(65, result.Progress);
        Assert.Equal("Building 3D Mesh Model", result.Stage);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task GetTaskStatusAsyncWhenHttpErrorReturnsFailure()
    {
        var taskId = Guid.NewGuid().ToString("D");
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Task not found", System.Text.Encoding.UTF8, "text/plain")
            }));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var result = await service.GetTaskStatusAsync(taskId);

        Assert.False(result.IsSuccess);
        Assert.Contains("NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task CancelTaskAsyncWhenSuccessfulReturnsTrue()
    {
        var taskId = Guid.NewGuid().ToString("D");
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains($"/api/v1/metashape/tasks/{taskId}/cancel", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var options = Options.Create(new MetashapeOptions { Endpoint = "http://localhost:8000" });
        var service = new MetashapeService(client, options, NullLogger<MetashapeService>.Instance);

        var result = await service.CancelTaskAsync(taskId);

        Assert.True(result);
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
