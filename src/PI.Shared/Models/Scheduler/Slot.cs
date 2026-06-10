using System;
using AutoMapper;

namespace PI.Shared.Models
{
    public class Slot
    {
        public Guid Id { get; set; }
        public int Start { get; set; }
        public int Duration { get; set; }
    }

    public class SlotProfile : Profile
    {
        public SlotProfile()
        {
            CreateMap<Availability, Slot>()
                .ForMember(dst => dst.Start, opts => opts.MapFrom(src => src.StartMinutes))
                .ForMember(dst => dst.Duration, opts => opts.MapFrom(src => src.DurationMinutes))
                ;
        }
    }
}