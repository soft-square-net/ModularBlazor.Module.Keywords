using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

namespace soft.square.net.WebApi.Modules.Keywords.UnitTests.Core.ConceptAggregate;

public class MatchKeyTests
{
    [Theory]
    [InlineData("القاهرة", "قاهره")]
    [InlineData("قاهره", "قاهره")]
    [InlineData("الْقَاهِرَة", "قاهره")]
    [InlineData("جامعة القاهرة", "جامعه قاهره")]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("إسلام", "اسلام")]
    [InlineData("آسيا", "اسيا")]
    [InlineData("Lionel MESSI", "lionel messi")]
    [InlineData("Égypte", "egypte")]
    public void FoldsTheWaysPeopleWriteAName(string text, string key) => MatchKey.For(text).ShouldBe(key);

    [Theory]
    [InlineData("ال")] // the article alone stays a word
    [InlineData("الم")] // fewer than two letters would be left
    public void KeepsShortWordsThatStartWithAl(string text) => MatchKey.For(text).ShouldBe(text);

    [Fact]
    public void AStoredSearchKeyGivesTheSameKey() =>
        MatchKey.FromTextKey(TextKey.For("القاهرة")).ShouldBe(MatchKey.For("القاهره"));

    [Theory]
    [InlineData("جامعه قاهره", "جامع")] // the longest word, its trailing ha dropped
    [InlineData("lionel messi", "lionel")]
    [InlineData("", "")]
    public void TheStemIsWhatEveryMatchingLabelContains(string key, string stem) => MatchKey.Stem(key).ShouldBe(stem);
}
