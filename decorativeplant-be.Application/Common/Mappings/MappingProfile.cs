using AutoMapper;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Auth mappings
        CreateMap<RegisterRequest, UserAccount>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true));

        CreateMap<RegisterCommand, UserAccount>()
             .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
             .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
             .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
             .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
             .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true));
             
        // Add more mappings as needed
    }
}
