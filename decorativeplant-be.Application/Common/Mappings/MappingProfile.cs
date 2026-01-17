using AutoMapper;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Auth mappings - UserAccount and UserProfile are created manually in handlers
        // No AutoMapper mappings needed as we're using direct entity creation

        // Add more mappings as needed for other entities
    }
}
