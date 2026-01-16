using System.ComponentModel.DataAnnotations.Schema;

namespace decorativeplant_be.Domain.Entities;

[Table("my_garden_plant")]
public class MyGardenPlant : BaseEntity
{
    public Guid UserId { get; set; }
    // Navigation property
    public UserAccount User { get; set; } = null!;

    public Guid? TaxonomyId { get; set; }
    public PlantTaxonomy? Taxonomy { get; set; }

    public Guid? SourceOrderItemId { get; set; }
    public OrderItem? SourceOrderItem { get; set; }

    public DateTime? AdoptedDate { get; set; }

    [Column(TypeName = "varchar(50)")]
    public string HealthStatus { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Nickname { get; set; }
}
