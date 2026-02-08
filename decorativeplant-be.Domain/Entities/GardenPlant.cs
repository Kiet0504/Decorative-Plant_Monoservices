using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// User's garden plant. JSONB: details. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class GardenPlant
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public JsonDocument? Details { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? CreatedAt { get; set; }

    public UserAccount? User { get; set; }
    public PlantTaxonomy? Taxonomy { get; set; }
    public ICollection<CareSchedule> CareSchedules { get; set; } = new List<CareSchedule>();
    public ICollection<CareLog> CareLogs { get; set; } = new List<CareLog>();
    public ICollection<PlantDiagnosis> PlantDiagnoses { get; set; } = new List<PlantDiagnosis>();
}
