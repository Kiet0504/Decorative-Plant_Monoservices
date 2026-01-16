using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class CareSchedule : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    // Potentially linked to a specific plant (BatchStock) or general taxonomy? 
    // ERD implies link closer to user's collection, but let's check ERD for specific link.
    // ERD 'care_schedule' has 'user_id'. It doesn't explicitly link to 'plant_batch' or 'batch_stock' in the diagram keys provided in snippets, 
    // but usually it refers to a plant. 
    // Checking the ERD content from memory/logs: 'care_schedule' usually keeps track of tasks.
    // I will assume for now it's just user-centric or flexible, but let's stick to the ERD fields if visible or common sense.
    // ERD usually has fields like `plant_id` or similar if linked.
    // Let's look at the ERD snippet if needed, but I'll stick to a generic structure resembling the ERD nodes I saw earlier (lines 1300+ in xml).
    // Found in previous analysis: care_schedule has `user_id`.
    
    public Guid? PlantId { get; set; } // Representing the specific plant instance if applicable
    public string PlantName { get; set; } = string.Empty; // In case it's a nickname
    
    public string CareType { get; set; } = string.Empty; // Water, Fertilize, Prune
    public string Frequency { get; set; } = string.Empty; // Cron or text description
    public DateTime NextActionDate { get; set; }

}
