using AutoMapper;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class ScheduledTaskProfile : Profile
    {
        public ScheduledTaskProfile()
        {
            CreateMap<PI.Shared.Models.PostEventScheduledTask, PostEventScheduledTask>();

            CreateMap<ScheduledTask, ScheduledTask>()
                .Include<PI.Shared.Models.PostEventScheduledTask, PostEventScheduledTask>()
                ;
        }
    }
}