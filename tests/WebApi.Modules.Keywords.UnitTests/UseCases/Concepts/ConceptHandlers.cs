using Ardalis.Specification;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.UnitTests.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.UseCases;
using soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

namespace soft.square.net.WebApi.Modules.Keywords.UnitTests.UseCases.Concepts;

/// <summary>The concept use cases over substituted repositories and platform contracts: no database, no host.</summary>
public class ConceptHandlers
{
    private readonly IRepository<Concept> _concepts = Substitute.For<IRepository<Concept>>();
    private readonly IAccessScopes _access = Substitute.For<IAccessScopes>();
    private readonly TestClock _clock = new();
    private readonly IOptions<KeywordsOptions> _options = Options.Create(new KeywordsOptions { MaxPageSize = 50 });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ConceptHandlers()
    {
        _access.UserId.Returns("user-1");
        _concepts.AddAsync(Arg.Any<Concept>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Concept>());
        _concepts.ListAsync(Arg.Any<ISpecification<Concept>>(), Arg.Any<CancellationToken>()).Returns([]);
        _concepts.ListAsync(Arg.Any<ISpecification<Concept, string>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private Concept Stored(int version = 0)
    {
        var concept = Concept.Create(ConceptTests.Messi(), "user-1", _clock.Now);
        for (var i = 0; i < version; i++) concept.Edit(ConceptTests.Messi("lionel-messi"), _clock.Now);
        _concepts.FirstOrDefaultAsync(Arg.Any<ISpecification<Concept>>(), Arg.Any<CancellationToken>()).Returns(concept);
        return concept;
    }

    [Fact]
    public async Task CreateNeedsASignedInCaller()
    {
        _access.UserId.Returns((string?)null);

        var result = await new CreateConceptHandler(_concepts, _access, _clock).Handle(new CreateConceptCommand(ConceptTests.Messi()), Ct);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task CreateNumbersATakenSlug()
    {
        _concepts.ListAsync(Arg.Any<ISpecification<Concept, string>>(), Arg.Any<CancellationToken>()).Returns(["lionel-messi", "lionel-messi-2"]);

        var result = await new CreateConceptHandler(_concepts, _access, _clock).Handle(new CreateConceptCommand(ConceptTests.Messi()), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Slug.ShouldBe("lionel-messi-3");
        result.Value.CreatedBy.ShouldBe("user-1");
    }

    [Fact]
    public async Task CreateRefusesAGivenSlugThatIsTaken()
    {
        _concepts.AnyAsync(Arg.Any<ISpecification<Concept>>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await new CreateConceptHandler(_concepts, _access, _clock).Handle(new CreateConceptCommand(ConceptTests.Messi("messi")), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _concepts.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task CreateRefusesRelatedConceptsThatDoNotExist()
    {
        var result = await new CreateConceptHandler(_concepts, _access, _clock)
            .Handle(new CreateConceptCommand(ConceptTests.Messi() with { Related = [41, 42] }), Ct);

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.Single().ErrorMessage.ShouldContain("41, 42");
    }

    [Fact]
    public async Task EditIsAConflictForAStaleVersion()
    {
        Stored(version: 2);

        var result = await new EditConceptHandler(_concepts, _clock).Handle(new EditConceptCommand(1, 1, ConceptTests.Messi()), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
    }

    [Fact]
    public async Task EditWithoutASlugKeepsTheCurrentOne()
    {
        Stored();

        var result = await new EditConceptHandler(_concepts, _clock)
            .Handle(new EditConceptCommand(1, 0, new ConceptValues("Person", [new("en", "Leo Messi")])), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Slug.ShouldBe("lionel-messi");
        result.Value.Name.ShouldBe("Leo Messi");
        await _concepts.Received(1).UpdateAsync(Arg.Any<Concept>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetireChangesTheStatus()
    {
        Stored();

        var result = await new SetConceptStatusHandler(_concepts, _clock).Handle(new SetConceptStatusCommand(1, 0, ConceptStatus.Retired), Ct);

        result.Value.Status.ShouldBe(ConceptStatus.Retired);
    }

    [Fact]
    public async Task ListChecksThePageAndTheType()
    {
        var reader = Substitute.For<IReadRepository<Concept>>();
        var handler = new ListConceptsHandler(reader, _options);

        (await handler.Handle(new ListConceptsQuery(PageSize: 51), Ct)).Status.ShouldBe(ResultStatus.Invalid);
        (await handler.Handle(new ListConceptsQuery(Type: "Footballer"), Ct)).Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task LookupKeepsTheOrderAskedAndSkipsUnknownIds()
    {
        var reader = Substitute.For<IReadRepository<Concept>>();
        var a = Concept.Create(new ConceptValues("Thing", [new("en", "A")]), "user-1", _clock.Now);
        var b = Concept.Create(new ConceptValues("Thing", [new("en", "B")]), "user-1", _clock.Now);
        typeof(Concept).GetProperty("Id")!.SetValue(a, 1);
        typeof(Concept).GetProperty("Id")!.SetValue(b, 2);
        reader.ListAsync(Arg.Any<ISpecification<Concept>>(), Arg.Any<CancellationToken>()).Returns([a, b]);

        var result = await new LookupConceptsHandler(reader, _options).Handle(new LookupConceptsQuery([2, 9, 1]), Ct);

        result.Value.Select(c => c.Name).ShouldBe(["B", "A"]);
    }
}
