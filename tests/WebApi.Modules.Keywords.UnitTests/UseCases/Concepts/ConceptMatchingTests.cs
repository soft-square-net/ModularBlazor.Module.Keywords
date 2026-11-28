using Ardalis.Specification;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

namespace soft.square.net.WebApi.Modules.Keywords.UnitTests.UseCases.Concepts;

/// <summary>How names are scored against concepts, and what POST /concepts/match accepts.</summary>
public class ConceptMatchingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private int _ids;

    private Concept Make(string type, params LabelValue[] labels)
    {
        var concept = Concept.Create(new ConceptValues(type, labels), "user-1", Now);
        typeof(Concept).GetProperty("Id")!.SetValue(concept, ++_ids);
        return concept;
    }

    [Theory]
    [InlineData("messi", "messi", 1)]
    [InlineData("messi", "messi lionel", 0.8)]
    [InlineData("lionel messi", "messi", 0.7)]
    [InlineData("mess", "lionel messi", 0.5)]
    [InlineData("me", "lionel messi", 0)]
    [InlineData("ronaldo", "lionel messi", 0)]
    public void ScoresHowAlikeTwoKeysAre(string term, string label, double score) => ConceptMatching.Score(term, label).ShouldBe(score);

    [Fact]
    public void RanksTheExactNameFirstWhateverTheArabicForm()
    {
        var cairo = Make("City", new LabelValue("en", "Cairo"), new LabelValue("ar", "القاهرة"));
        var university = Make("Organization", new LabelValue("en", "Cairo University"), new LabelValue("ar", "جامعة القاهرة"));

        var result = ConceptMatching.Rank("قاهره", MatchKey.For("قاهره"), [university, cairo], "ar");

        result.Exact.ShouldBeTrue();
        result.Matches.Select(m => (m.Id, m.Name, m.Score)).ShouldBe([(cairo.Id, "القاهرة", 1.0), (university.Id, "جامعة القاهرة", 0.7)]);
    }

    [Fact]
    public void AHiddenNameMatchesButIsNotExact()
    {
        var messi = Make("Person", new LabelValue("en", "Lionel Messi"), new LabelValue("en", "Mesi", LabelKind.Hidden));

        var result = ConceptMatching.Rank("mesi", "mesi", [messi], "en");

        result.Exact.ShouldBeFalse();
        result.Matches.Single().Score.ShouldBe(0.9);
    }

    [Fact]
    public void KeepsTheBestFive()
    {
        var concepts = Enumerable.Range(1, 8).Select(i => Make("Thing", new LabelValue("en", $"Topic {new string('x', i)}"))).ToList();

        var result = ConceptMatching.Rank("topic", "topic", concepts, null);

        result.Matches.Count.ShouldBe(ConceptMatching.MaxMatches);
        result.Matches[0].Name.ShouldBe("Topic x"); // shorter names first among equal scores
        result.Exact.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswersEveryTermInTheOrderAsked()
    {
        var concepts = Substitute.For<IReadRepository<Concept>>();
        var cairo = Make("City", new LabelValue("en", "Cairo"), new LabelValue("ar", "القاهرة"));
        concepts.ListAsync(Arg.Any<ISpecification<Concept>>(), Arg.Any<CancellationToken>()).Returns([cairo]);

        var result = await new MatchConceptsHandler(concepts).Handle(new MatchConceptsQuery(["Cairo", " ", "Cairo", "Alexandria"], "en"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(t => t.Term).ShouldBe(["Cairo", "Alexandria"]); // blanks and repeats dropped
        result.Value[0].Exact.ShouldBeTrue();
        result.Value[0].Matches.Single().Name.ShouldBe("Cairo");
        result.Value[1].Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefusesTooManyTermsNoTermsAndUnknownTypes()
    {
        var handler = new MatchConceptsHandler(Substitute.For<IReadRepository<Concept>>());

        (await handler.Handle(new MatchConceptsQuery([.. Enumerable.Range(0, 21).Select(i => $"t{i}")]), Ct)).Status.ShouldBe(ResultStatus.Invalid);
        (await handler.Handle(new MatchConceptsQuery([]), Ct)).Status.ShouldBe(ResultStatus.Invalid);
        (await handler.Handle(new MatchConceptsQuery(["x"], Type: "Planet"), Ct)).Status.ShouldBe(ResultStatus.Invalid);
    }
}
