using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Application.Features.VersionHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Harvests;

public sealed class HarvestQualityGradeCommandHandlerTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now =
        CreatedAt.AddHours(1);

    [Fact]
    public async Task CreateAddsFirstRevisionAndSavesOnce()
    {
        var repository = new FakeHarvestQualityGradeRepository();
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new CreateHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateHarvestQualityGradeCommand(
                " premium ",
                " Premium ",
                1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PREMIUM", result.Value.Code);
        Assert.Equal(1, result.Value.RevisionNumber);
        Assert.True(result.Value.IsActive);
        Assert.Single(repository.AddedGrades);
        Assert.Equal(1, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task CreateRejectsExistingCode()
    {
        var repository = new FakeHarvestQualityGradeRepository
        {
            CodeExists = true
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new CreateHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateHarvestQualityGradeCommand(
                "PREMIUM",
                "Premium",
                1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "HarvestQualityGrade.CodeAlreadyExists",
            result.Error.Code);
        Assert.Empty(repository.AddedGrades);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task VersionRetiresCurrentAndCreatesNextVersionAtomically()
    {
        var current = CreateGrade();
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = current
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new VersionHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VersionHarvestQualityGradeCommand(
                current.Id,
                "Premium fruit",
                2,
                current.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(current.IsActive);
        Assert.Equal(Now, current.RetiredAt);
        Assert.Equal(2, current.Version);
        Assert.Equal(1, repository.UpdateCallCount);

        var next = Assert.Single(repository.AddedGrades);
        Assert.Equal(current.Id, next.SupersedesId);
        Assert.Equal(2, next.RevisionNumber);
        Assert.True(next.IsActive);
        Assert.Equal(next.Id, result.Value.Id);
        Assert.Equal(1, unitOfWork.TransactionCallCount);
        Assert.Equal(2, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task VersionRejectsStaleExpectedVersion()
    {
        var current = CreateGrade();
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = current
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new VersionHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VersionHarvestQualityGradeCommand(
                current.Id,
                "Premium fruit",
                2,
                ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "HarvestQualityGrade.ConcurrentUpdate",
            result.Error.Code);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task RetireMarksActiveGradeInactiveAndSavesOnce()
    {
        var grade = CreateGrade();
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = grade
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new RetireHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetireHarvestQualityGradeCommand(
                grade.Id,
                grade.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(grade.IsActive);
        Assert.Equal(Now, grade.RetiredAt);
        Assert.Equal(2, grade.Version);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(1, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task RetireRejectsAlreadyRetiredGrade()
    {
        var grade = CreateGrade();
        grade.Retire(Now.AddMinutes(-1));
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = grade
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new RetireHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetireHarvestQualityGradeCommand(
                grade.Id,
                grade.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "HarvestQualityGrade.AlreadyRetired",
            result.Error.Code);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task RetireRejectsStaleExpectedVersion()
    {
        var grade = CreateGrade();
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = grade
        };
        var unitOfWork = new FakeHarvestsUnitOfWork();
        var handler = new RetireHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetireHarvestQualityGradeCommand(
                grade.Id,
                ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "HarvestQualityGrade.ConcurrentUpdate",
            result.Error.Code);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task RetireMapsDatabaseConcurrencyFailureToConflict()
    {
        var grade = CreateGrade();
        var repository = new FakeHarvestQualityGradeRepository
        {
            Grade = grade
        };
        var unitOfWork = new FakeHarvestsUnitOfWork
        {
            SaveException = new DbUpdateConcurrencyException()
        };
        var handler = new RetireHarvestQualityGradeHandler(
            repository,
            unitOfWork,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetireHarvestQualityGradeCommand(
                grade.Id,
                grade.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "HarvestQualityGrade.ConcurrentUpdate",
            result.Error.Code);
    }

    private static HarvestQualityGrade CreateGrade() =>
        HarvestQualityGrade.Create(
            "PREMIUM",
            "Premium",
            1,
            CreatedAt);

    private sealed class FakeHarvestQualityGradeRepository
        : IHarvestQualityGradeRepository
    {
        public HarvestQualityGrade? Grade { get; init; }
        public bool CodeExists { get; init; }
        public List<HarvestQualityGrade> AddedGrades { get; } = [];
        public int UpdateCallCount { get; private set; }

        public Task<HarvestQualityGrade?> GetByIdAsync(
            Guid gradeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Grade);

        public Task<bool> CodeExistsAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CodeExists);

        public void Add(HarvestQualityGrade grade) =>
            AddedGrades.Add(grade);

        public void Update(HarvestQualityGrade grade) =>
            UpdateCallCount++;
    }

    private sealed class FakeHarvestsUnitOfWork : IHarvestsUnitOfWork
    {
        public int SaveCallCount { get; private set; }
        public int TransactionCallCount { get; private set; }
        public Exception? SaveException { get; init; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.FromResult(1);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCallCount++;
            return await operation(cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
