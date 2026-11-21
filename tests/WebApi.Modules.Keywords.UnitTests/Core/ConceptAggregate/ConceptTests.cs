using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

namespace soft.square.net.WebApi.Modules.Keywords.UnitTests.Core.ConceptAggregate;

public class TextKeyTests
{
    [Theory]
    [InlineData("Lionel MESSI", "lionel messi")]
    [InlineData("  Café   Müller ", "cafe muller")]
    [InlineData("Real-Madrid, C.F.", "real madrid c f")]
    [InlineData("مُحَمَّد", "محمد")] // harakat and shadda
    [InlineData("القاهـــرة", "القاهرة")] // tatweel; ta marbuta is kept
    [InlineData("أحمد", "احمد")]
    [InlineData("إسلام", "اسلام")]
    [InlineData("آمال", "امال")]
    [InlineData("مستشفى", "مستشفي")] // alef maqsura → ya
    [InlineData("كأس ٢٠٢٦", "كاس 2026")]
    public void FoldsTheWaysPeopleTypeOneName(string text, string key) => TextKey.For(text).ShouldBe(key);

    [Fact]
    public void KeepsTaMarbutaAndHaApart() => TextKey.For("مدرسة").ShouldNotBe(TextKey.For("مدرسه"));

    [Fact]
    public void BlankIsEmpty() => TextKey.For("  \t ").ShouldBe("");
}

public class ConceptRulesTests
{
    [Theory]
    [InlineData("Lionel Messi", "lionel-messi")]
    [InlineData("  Café de Flore! ", "cafe-de-flore")]
    [InlineData("القاهرة الكبرى", "القاهرة-الكبرى")]
    [InlineData("!!!", "concept")]
    [InlineData("FIFA World Cup 2026", "fifa-world-cup-2026")]
    public void SlugifiesNames(string name, string slug) => ConceptRules.Slugify(name).ShouldBe(slug);

    [Theory]
    [InlineData("Has Space")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("")]
    public void RejectsBadSlugs(string slug) => Should.Throw<DomainException>(() => ConceptRules.CheckSlug(slug));

    [Fact]
    public void NeedsAPreferredLabel() =>
        Should.Throw<DomainException>(() => ConceptRules.CheckLabels([new("en", "Messi", LabelKind.Alternate)]))
            .Message.ShouldContain("preferred");

    [Fact]
    public void AllowsOnePreferredLabelPerLanguage() =>
        Should.Throw<DomainException>(() => ConceptRules.CheckLabels([new("en", "Messi"), new("en", "Leo Messi")]))
            .Message.ShouldContain("One preferred label per language");

    [Fact]
    public void RejectsTheSameLabelTwiceAfterFolding() =>
        Should.Throw<DomainException>(() => ConceptRules.CheckLabels([new("ar", "أحمد"), new("ar", "احمد", LabelKind.Alternate)]));

    [Fact]
    public void CleansLabelsAndPutsPreferredFirst()
    {
        var labels = ConceptRules.CheckLabels([new("EN", " Leo ", LabelKind.Alternate), new("en", "Lionel Messi"), new("ar", ""), new("ar", "ليونيل ميسي")]);

        labels.ShouldBe([new("en", "Lionel Messi"), new("ar", "ليونيل ميسي"), new("en", "Leo", LabelKind.Alternate)]);
    }

    [Theory]
    [InlineData("english")]
    [InlineData("e")]
    [InlineData("en_GB")]
    public void RejectsBadLanguageTags(string language) => Should.Throw<DomainException>(() => ConceptRules.CheckLanguage(language));

    [Fact]
    public void SameAsIsHttpOnlyWithoutRepeats()
    {
        ConceptRules.CheckSameAs(["https://www.wikidata.org/wiki/Q615", " ", "https://www.wikidata.org/wiki/Q615"]).ShouldBe(["https://www.wikidata.org/wiki/Q615"]);
        Should.Throw<DomainException>(() => ConceptRules.CheckSameAs(["javascript:alert(1)"]));
        Should.Throw<DomainException>(() => ConceptRules.CheckSameAs(["wikidata.org/wiki/Q615"]));
    }

    [Fact]
    public void RelatedIsNotItself()
    {
        ConceptRules.CheckRelated([3, 3, 4], self: 7).ShouldBe([3, 4]);
        Should.Throw<DomainException>(() => ConceptRules.CheckRelated([7], self: 7));
    }
}

public class ConceptTests
{
    private static readonly DateTimeOffset Now = TestClock.Start;

    public static ConceptValues Messi(string? slug = null) => new("Person",
        [new("en", "Lionel Messi"), new("ar", "ليونيل ميسي"), new("en", "Leo Messi", LabelKind.Alternate), new("en", "Mesi", LabelKind.Hidden)],
        slug, "Argentine footballer.", ["https://www.wikidata.org/wiki/Q615"]);

    [Fact]
    public void CreatesWithANameAndASlugFromTheFirstPreferredLabel()
    {
        var concept = Concept.Create(Messi(), "user-1", Now);

        concept.Name.ShouldBe("Lionel Messi");
        concept.Slug.ShouldBe("lionel-messi");
        concept.Status.ShouldBe(ConceptStatus.Active);
        concept.Labels.Select(l => l.SearchKey).ShouldContain("ليونيل ميسي");
    }

    [Fact]
    public void RejectsAnUnknownType() =>
        Should.Throw<DomainException>(() => Concept.Create(Messi() with { Type = "Footballer" }, "user-1", Now));

    [Fact]
    public void NamesInALanguageOrFallsBack()
    {
        var concept = Concept.Create(Messi(), "user-1", Now);

        concept.NameIn("ar").ShouldBe("ليونيل ميسي");
        concept.NameIn("ar-EG").ShouldBe("ليونيل ميسي");
        concept.NameIn("fr").ShouldBe("Lionel Messi");
        concept.NameIn(null).ShouldBe("Lionel Messi");
    }

    [Fact]
    public void EditReplacesTheLabelsAndBumpsTheVersion()
    {
        var concept = Concept.Create(Messi(), "user-1", Now);

        concept.Edit(new ConceptValues("Person", [new("en", "Messi")], "messi"), Now.AddMinutes(1));

        concept.Labels.Select(l => l.Text).ShouldBe(["Messi"]);
        concept.Slug.ShouldBe("messi");
        concept.SameAs.ShouldBeEmpty();
        concept.Version.ShouldBe(1);
        concept.UpdatedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void RetiresAndRestoresOnce()
    {
        var concept = Concept.Create(Messi(), "user-1", Now);

        concept.Retire(Now);
        Should.Throw<DomainException>(() => concept.Retire(Now));
        concept.Restore(Now);
        Should.Throw<DomainException>(() => concept.Restore(Now));
        concept.Version.ShouldBe(2);
    }

    [Fact]
    public void JsonLdShowsTheVisibleNamesAndLinks()
    {
        var json = ConceptJsonLd.For(Concept.Create(Messi(), "user-1", Now), "ar");

        json["@context"].ShouldBe("https://schema.org");
        json["@type"].ShouldBe("Person");
        json["name"].ShouldBe("ليونيل ميسي");
        ((IEnumerable<string>)json["alternateName"]!).ShouldBe(["Lionel Messi", "Leo Messi"]); // never the hidden "Mesi"
        ((IEnumerable<string>)json["sameAs"]!).ShouldBe(["https://www.wikidata.org/wiki/Q615"]);
        json["identifier"].ShouldBe("lionel-messi");
    }

    [Fact]
    public void ATopicHasATermCode()
    {
        var json = ConceptJsonLd.For(Concept.Create(new ConceptValues(ConceptTypes.DefinedTerm, [new("en", "Sports")]), "user-1", Now), null);

        json["termCode"].ShouldBe("sports");
        json.ShouldNotContainKey("identifier");
    }

    [Fact]
    public void TypesKnowTheirAncestors()
    {
        ConceptTypes.IsA("City", "Place").ShouldBeTrue();
        ConceptTypes.IsA("City", "Thing").ShouldBeTrue();
        ConceptTypes.IsA("City", "Organization").ShouldBeFalse();
        ConceptTypes.All.Where(t => t.Parent is not null).ShouldAllBe(t => ConceptTypes.Find(t.Parent) != null);
    }
}
