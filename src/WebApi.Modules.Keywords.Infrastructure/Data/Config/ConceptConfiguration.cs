using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

namespace soft.square.net.WebApi.Modules.Keywords.Infrastructure.Data.Config;

public sealed class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> concept)
    {
        concept.ToTable("concepts");
        concept.Property(c => c.Slug).HasMaxLength(ConceptRules.SlugMaxLength);
        // A slug is unique per tenant (the platform's TenantDbContext maps TenantId: 64 characters, indexed).
        concept.HasIndex(nameof(Concept.TenantId), nameof(Concept.Slug)).IsUnique();
        concept.Property(c => c.Type).HasMaxLength(64);
        concept.Property(c => c.Name).HasMaxLength(ConceptRules.LabelMaxLength);
        concept.HasIndex(c => c.Name);
        concept.Property(c => c.Description).HasMaxLength(ConceptRules.DescriptionMaxLength);
        concept.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        // Unbounded: 20 URLs of 500 characters are more than any engine's ordinary string column (Oracle: NCLOB).
        concept.Property(c => c.SameAsJson);
        concept.Property(c => c.RelatedJson).HasMaxLength(1000);
        concept.Property(c => c.CreatedBy).HasMaxLength(DataSchemaConstants.UserIdLength);
        concept.Property(c => c.Version).IsConcurrencyToken();
        concept.Ignore(c => c.SameAs);
        concept.Ignore(c => c.Related);

        concept.HasMany(c => c.Labels).WithOne().HasForeignKey("ConceptId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        concept.Navigation(c => c.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ConceptLabelConfiguration : IEntityTypeConfiguration<ConceptLabel>
{
    public void Configure(EntityTypeBuilder<ConceptLabel> label)
    {
        label.ToTable("concept_labels");
        label.Property(l => l.Language).HasMaxLength(ConceptRules.LanguageMaxLength);
        label.Property(l => l.Text).HasMaxLength(ConceptRules.LabelMaxLength);
        label.Property(l => l.Kind).HasConversion<string>().HasMaxLength(16);
        // Folding only drops case and marks; room to spare for the rare expansion.
        label.Property(l => l.SearchKey).HasMaxLength(ConceptRules.LabelMaxLength * 2);
        label.HasIndex(l => l.SearchKey);
    }
}

/// <summary>Column sizes shared by the mappings.</summary>
public static class DataSchemaConstants
{
    /// <summary>A user id (the token's <c>sub</c>).</summary>
    public const int UserIdLength = 128;
}
