using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class AcceptanceCriteriaVerificationTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<(long ProjectId, int WpNumber)> CreateProjectAndWpAsync(CancellationToken ct)
    {
        var projResponse = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/ac-verify-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await projResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);

        await Client.PostAsJsonAsync($"{BasePath}/{project!.Id}/work-packages", new CreateWorkPackageRequest
        {
            Name = "Test WP",
            Description = "Test"
        }, ct);

        return (project.Id, 1);
    }

    private string PhasePath(long projectId, int wpNumber) =>
        $"{BasePath}/{projectId}/work-packages/{wpNumber}/phases";

    private string VerifyPath(long projectId, int wpNumber, int phaseNumber) =>
        $"{PhasePath(projectId, wpNumber)}/{phaseNumber}/verify";

    private async Task<PhaseResponse> CreatePhaseWithCriteriaAsync(long projectId, int wpNumber, CancellationToken ct)
    {
        var response = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase with AC",
            AcceptanceCriteria =
            [
                new AcceptanceCriterionDto
                {
                    Name = "Tests pass",
                    Description = "All unit tests green",
                    VerificationMethod = Shared.Enums.VerificationMethod.AutomatedTest
                },
                new AcceptanceCriterionDto
                {
                    Name = "Code reviewed",
                    Description = "Peer review completed",
                    VerificationMethod = Shared.Enums.VerificationMethod.Manual
                }
            ]
        }, ct);
        return (await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct))!;
    }

    [Fact]
    public async Task Post_VerifiesCriteria_SetsResultAndTimestamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await CreatePhaseWithCriteriaAsync(projectId, wpNumber, ct);

        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria =
                [
                    new VerifyCriterionItem { Name = "Tests pass", VerificationResult = "PASS: All 15 tests passed" }
                ]
            }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        var criterion = phase!.AcceptanceCriteria.First(ac => ac.Name == "Tests pass");
        Assert.Equal("PASS: All 15 tests passed", criterion.VerificationResult);
        Assert.NotNull(criterion.VerifiedAt);
    }

    [Fact]
    public async Task Post_VerifiesMultipleCriteria_InSingleCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await CreatePhaseWithCriteriaAsync(projectId, wpNumber, ct);

        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria =
                [
                    new VerifyCriterionItem { Name = "Tests pass", VerificationResult = "PASS: All tests green" },
                    new VerifyCriterionItem { Name = "Code reviewed", VerificationResult = "PASS: Approved by reviewer" }
                ]
            }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        Assert.All(phase!.AcceptanceCriteria, ac =>
        {
            Assert.NotNull(ac.VerificationResult);
            Assert.NotNull(ac.VerifiedAt);
        });
    }

    [Fact]
    public async Task Post_ReVerification_UpdatesResultAndTimestamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await CreatePhaseWithCriteriaAsync(projectId, wpNumber, ct);

        // First verification
        await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria = [new VerifyCriterionItem { Name = "Tests pass", VerificationResult = "FAIL: 2 tests failed" }]
            }, ct);

        // Re-verification
        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria = [new VerifyCriterionItem { Name = "Tests pass", VerificationResult = "PASS: All tests fixed" }]
            }, ct);

        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
        var criterion = phase!.AcceptanceCriteria.First(ac => ac.Name == "Tests pass");
        Assert.Equal("PASS: All tests fixed", criterion.VerificationResult);
    }

    [Fact]
    public async Task Post_InvalidCriterionName_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await CreatePhaseWithCriteriaAsync(projectId, wpNumber, ct);

        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria = [new VerifyCriterionItem { Name = "Nonexistent criterion", VerificationResult = "PASS" }]
            }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CaseInsensitiveMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await CreatePhaseWithCriteriaAsync(projectId, wpNumber, ct);

        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 1),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria = [new VerifyCriterionItem { Name = "tests PASS", VerificationResult = "PASS: Case insensitive" }]
            }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
        var criterion = phase!.AcceptanceCriteria.First(ac => ac.Name == "Tests pass");
        Assert.Equal("PASS: Case insensitive", criterion.VerificationResult);
    }

    [Fact]
    public async Task Post_Returns404_WhenPhaseNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        var response = await Client.PostAsJsonAsync(VerifyPath(projectId, wpNumber, 999),
            new VerifyAcceptanceCriteriaRequest
            {
                Criteria = [new VerifyCriterionItem { Name = "Anything", VerificationResult = "PASS" }]
            }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
