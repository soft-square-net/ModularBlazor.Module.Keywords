using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate.Specifications;
using soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

namespace soft.square.net.WebApi.Modules.Keywords.IntegrationTests.Data;

/// <summary>The module's mapping and specifications through the platform's repository, on a real database.</summary>
public sealed class EfRepositoryConcepts : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
    private readonly KeywordsDatabase _database = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private async Task<Concept> AddAsync(string type, string tenant = "acme", params LabelValue[] labels)
    {
        await using var context = _database.For(tenant);
        return await new EfRepository<Concept>(context).AddAsync(Concept.Create(new ConceptValues(type, labels), "user-1", Now), Ct);
    }

    [Fact]
    public async Task StoresAConceptWithItsLabelsForItsTenantOnly()
    {
        var saved = await AddAsync("Person", "acme", new LabelValue("en", "Lionel Messi"), new LabelValue("ar", "ليونيل ميسي"));

        await using (var acme = _database.For("acme"))
        {
            var concept = await new EfRepository<Concept>(acme).FirstOrDefaultAsync(new ConceptReadSpec(saved.Id), Ct);
            concept.ShouldNotBeNull();
            concept.TenantId.ShouldBe("acme");
            concept.Labels.Count.ShouldBe(2);
        }
        await using (var globex = _database.For("globex"))
            (await new EfRepository<Concept>(globex).FirstOrDefaultAsync(new ConceptReadSpec(saved.Id), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task SearchFindsAnyLabelWhateverTheArabicForm()
    {
        await AddAsync("Person", "acme", new LabelValue("en", "Ahmed Zewail"), new LabelValue("ar", "أحمد زويل"));
        await AddAsync("Person", "acme", new LabelValue("en", "Ahmad Shawqi"), new LabelValue("ar", "أَحْمَد شَوْقِي"));
        await AddAsync("City", "acme", new LabelValue("en", "Cairo"), new LabelValue("ar", "القاهرة"));
        await AddAsync("Person", "globex", new LabelValue("en", "Ahmed Other"), new LabelValue("ar", "احمد"));

        await using var context = _database.For("acme");
        var concepts = new EfRepository<Concept>(context);
        var spec = new ConceptsPageSpec(1, 10, search: "احمد");

        (await concepts.ListAsync(spec, Ct)).Select(c => c.Name).ShouldBe(["Ahmad Shawqi", "Ahmed Zewail"]);
        (await concepts.CountAsync(spec, Ct)).ShouldBe(2);
        (await concepts.ListAsync(new ConceptsPageSpec(1, 10, search: "CAIRO", type: "City"), Ct)).Single().Name.ShouldBe("Cairo");
        (await concepts.ListAsync(new ConceptsPageSpec(1, 10, search: "cairo", type: "Person"), Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchTurnsNamesIntoConceptsOfTheTenant()
    {
        var cairo = await AddAsync("City", "acme", new LabelValue("en", "Cairo"), new LabelValue("ar", "القاهرة"));
        var university = await AddAsync("Organization", "acme", new LabelValue("en", "Cairo University"), new LabelValue("ar", "جامعة القاهرة"));
        await AddAsync("City", "globex", new LabelValue("en", "Cairo"), new LabelValue("ar", "القاهرة"));
        var retired = await AddAsync("Thing", "acme", new LabelValue("en", "Old Cairo"));
        await using (var context = _database.For("acme"))
        {
            var concepts = new EfRepository<Concept>(context);
            var concept = await concepts.FirstOrDefaultAsync(new ConceptByIdSpec(retired.Id), Ct);
            concept!.Retire(Now);
            await concepts.UpdateAsync(concept, Ct);
        }

        await using var reader = _database.For("acme");
        var result = await new MatchConceptsHandler(new EfRepository<Concept>(reader))
            .Handle(new MatchConceptsQuery(["قاهره", "cairo university", "Alexandria"], "ar"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value[0].Exact.ShouldBeTrue();
        result.Value[0].Matches.Select(m => m.Id).ShouldBe([cairo.Id, university.Id]); // not globex's, not the retired one
        result.Value[0].Matches[0].Name.ShouldBe("القاهرة");
        result.Value[1].Matches[0].Id.ShouldBe(university.Id);
        result.Value[1].Exact.ShouldBeTrue();
        result.Value[2].Matches.ShouldBeEmpty();
        result.Value[2].Exact.ShouldBeFalse();

        var cities = await new MatchConceptsHandler(new EfRepository<Concept>(reader)).Handle(new MatchConceptsQuery(["Cairo"], "en", "City"), Ct);
        cities.Value.Single().Matches.Select(m => m.Id).ShouldBe([cairo.Id]);
    }

    [Fact]
    public async Task RetiredConceptsAreLeftOutUnlessAsked()
    {
        var saved = await AddAsync("Thing", "acme", new LabelValue("en", "Old thing"));
        await using (var context = _database.For("acme"))
        {
            var concepts = new EfRepository<Concept>(context);
            var concept = await concepts.FirstOrDefaultAsync(new ConceptByIdSpec(saved.Id), Ct);
            concept!.Retire(Now);
            await concepts.UpdateAsync(concept, Ct);
        }

        await using var reader = _database.For("acme");
        (await new EfRepository<Concept>(reader).CountAsync(new ConceptsPageSpec(1, 10), Ct)).ShouldBe(0);
        (await new EfRepository<Concept>(reader).CountAsync(new ConceptsPageSpec(1, 10, includeRetired: true), Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task EditReplacesTheStoredLabels()
    {
        var saved = await AddAsync("Thing", "acme", new LabelValue("en", "One"), new LabelValue("en", "Uno", LabelKind.Alternate));
        await using (var context = _database.For("acme"))
        {
            var concepts = new EfRepository<Concept>(context);
            var concept = await concepts.FirstOrDefaultAsync(new ConceptByIdSpec(saved.Id), Ct);
            concept!.Edit(new ConceptValues("Thing", [new("en", "Two"), new("fr", "Deux")], concept.Slug), Now);
            await concepts.UpdateAsync(concept, Ct);
        }

        await using var reader = _database.For("acme");
        var stored = await new EfRepository<Concept>(reader).FirstOrDefaultAsync(new ConceptReadSpec(saved.Id), Ct);
        stored!.Labels.Select(l => l.Text).Order().ShouldBe(["Deux", "Two"]);
        (await reader.Set<ConceptLabel>().CountAsync(Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task ASlugIsUniquePerTenant()
    {
        await AddAsync("Thing", "acme", new LabelValue("en", "Same"));
        await AddAsync("Thing", "globex", new LabelValue("en", "Same")); // another tenant: fine

        await Should.ThrowAsync<DbUpdateException>(() => AddAsync("Thing", "acme", new LabelValue("en", "Same")));

        await using var context = _database.For("acme");
        (await new EfRepository<Concept>(context).ListAsync(new SlugsLikeSpec("same"), Ct)).ShouldBe(["same"]);
    }

    [Fact]
    public async Task TheVersionIsAConcurrencyToken()
    {
        var saved = await AddAsync("Thing", "acme", new LabelValue("en", "Title"));

        await using var first = _database.For("acme");
        await using var second = _database.For("acme");
        var mine = await new EfRepository<Concept>(first).FirstOrDefaultAsync(new ConceptByIdSpec(saved.Id), Ct);
        var theirs = await new EfRepository<Concept>(second).FirstOrDefaultAsync(new ConceptByIdSpec(saved.Id), Ct);

        mine!.Retire(Now);
        await new EfRepository<Concept>(first).UpdateAsync(mine, Ct);

        theirs!.Retire(Now);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => new EfRepository<Concept>(second).UpdateAsync(theirs, Ct));
    }
}
