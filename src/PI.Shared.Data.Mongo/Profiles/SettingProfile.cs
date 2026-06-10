using AutoMapper;
using PI.Shared.Data.Models;

namespace PI.Shared.Data.Mongo
{
    public class SettingProfile : Profile
    {
        public SettingProfile()
        {
            CreateMap<Setting, TextSetting>(MemberList.Source);  
        }
    }
}