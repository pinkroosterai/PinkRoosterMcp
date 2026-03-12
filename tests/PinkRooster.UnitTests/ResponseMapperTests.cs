using PinkRooster.Api.Services;
using PinkRooster.Data.Entities;
using Xunit;

namespace PinkRooster.UnitTests;

public sealed class ResponseMapperTests
{
    [Fact]
    public void MapFileReference_MapsAllFields()
    {
        var entity = new FileReference
        {
            FileName = "test.cs",
            RelativePath = "src/test.cs",
            Description = "A test file"
        };

        var dto = ResponseMapper.MapFileReference(entity);

        Assert.Equal("test.cs", dto.FileName);
        Assert.Equal("src/test.cs", dto.RelativePath);
        Assert.Equal("A test file", dto.Description);
    }

    [Fact]
    public void MapFileReference_NullDescription_MapsAsNull()
    {
        var entity = new FileReference
        {
            FileName = "test.cs",
            RelativePath = "src/test.cs",
            Description = null
        };

        var dto = ResponseMapper.MapFileReference(entity);

        Assert.Null(dto.Description);
    }

    [Fact]
    public void MapFileReferences_MultipleFiles_MapsAll()
    {
        var entities = new List<FileReference>
        {
            new() { FileName = "a.cs", RelativePath = "src/a.cs" },
            new() { FileName = "b.cs", RelativePath = "src/b.cs", Description = "File B" }
        };

        var dtos = ResponseMapper.MapFileReferences(entities);

        Assert.Equal(2, dtos.Count);
        Assert.Equal("a.cs", dtos[0].FileName);
        Assert.Equal("b.cs", dtos[1].FileName);
        Assert.Equal("File B", dtos[1].Description);
    }

    [Fact]
    public void MapFileReferences_EmptyList_ReturnsEmpty()
    {
        var dtos = ResponseMapper.MapFileReferences([]);
        Assert.Empty(dtos);
    }

    [Fact]
    public void MapAcceptanceCriterion_MapsAllFields()
    {
        var ac = new AcceptanceCriterion
        {
            Name = "Test criterion",
            Description = "Must pass",
            VerificationMethod = PinkRooster.Shared.Enums.VerificationMethod.AutomatedTest,
            VerificationResult = "Passed",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        var dto = ResponseMapper.MapAcceptanceCriterion(ac);

        Assert.Equal("Test criterion", dto.Name);
        Assert.Equal("Must pass", dto.Description);
        Assert.Equal(PinkRooster.Shared.Enums.VerificationMethod.AutomatedTest, dto.VerificationMethod);
        Assert.Equal("Passed", dto.VerificationResult);
        Assert.NotNull(dto.VerifiedAt);
    }

    [Fact]
    public void MapAcceptanceCriterion_NullOptionalFields()
    {
        var ac = new AcceptanceCriterion
        {
            Name = "Criterion",
            Description = "Desc",
            VerificationMethod = PinkRooster.Shared.Enums.VerificationMethod.Manual,
            VerificationResult = null,
            VerifiedAt = null
        };

        var dto = ResponseMapper.MapAcceptanceCriterion(ac);

        Assert.Null(dto.VerificationResult);
        Assert.Null(dto.VerifiedAt);
    }
}
