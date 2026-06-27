using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureListEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CaptureListEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCaptures_ReturnsOk()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/captures");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();
        body!.Captures.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCaptures_AfterRegister_ContainsCapture()
    {
        // Seed a capture directly
        var capture = Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64]));

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();
        await repo.AddAsync(capture);

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/captures");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        body!.Captures.Should().Contain(c => c.CaptureId == capture.Id.Value);
    }

    [Fact]
    public async Task GetCaptures_WithoutAuth_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/captures");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCaptures_WithPageSize1_ReturnsSingleItemAndCorrectPagination()
    {
        // Seed two captures
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaptureRepository>();
        await repo.AddAsync(Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64])));
        await repo.AddAsync(Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64])));

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/captures?page=1&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        body!.Captures.Should().HaveCount(1);
        body.PageSize.Should().Be(1);
        body.Page.Should().Be(1);
        body.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        body.TotalPages.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetCaptures_DefaultPageSize_ReturnsPaginationMetadata()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/captures");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(50);
        body.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
