using System;
using AutoMapper;
using MongoDB.Bson;

namespace PI.ProductCatalog.Models;

public class CatalogItemProfile : Profile
{
    public CatalogItemProfile()
    {
        CreateMap<LINCTP, ItemCost>();
        CreateMap<SLNCTP, ItemCost>(MemberList.Source);

        CreateMap<LIN, CatalogStyleUpdate>(MemberList.Source)
            .ForSourceMember(s => s.CTPs, o => o.DoNotValidate())
            .ForSourceMember(s => s.Items, o => o.DoNotValidate())
            .ForSourceMember(s => s.MaterialClassification, o => o.DoNotValidate())
            .ForMember(dst => dst.Material, o => o.MapFrom(src => MaterialClassification.Parse(src.MaterialClassification)))
            ;

        CreateMap<LIN, CatalogItemUpdate>(MemberList.Source)
            .ForSourceMember(s => s.CTPs, o => o.DoNotValidate())
            .ForSourceMember(s => s.Items, o => o.DoNotValidate())
            .ForSourceMember(s => s.MaterialClassification, o => o.DoNotValidate())
            .ForMember(dst => dst.Material, o => o.MapFrom(src => MaterialClassification.Parse(src.MaterialClassification)))
            ;

        CreateMap<CatalogItemUpdate, CatalogItem>()
            .ForMember(x => x.CatalogFeedId, o => o.Ignore())
            .ForMember(x => x.Id, o => o.MapFrom(s => ObjectId.GenerateNewId().ToGuid()))
            // .ForMember(x => x.Name, o => o.MapFrom(s => s.GetName()))
            .ForMember(x => x.CreatedOn, o => o.MapFrom(s => DateTime.UtcNow))
            .ForMember(x => x.EntityId, o => o.Ignore())
            .ForMember(x => x.AccountId, o => o.Ignore())
            .ForMember(x => x.LastModifiedOn, o => o.Ignore())
            .ForMember(x => x.LastActor, o => o.Ignore())
            .ForMember(x => x.ParentIds, o => o.Ignore())
            .ForMember(x => x.Parents, o => o.Ignore())
            .ForMember(x => x.Description, o => o.Ignore())
            .ForMember(x => x.Salesforce, o => o.Ignore())

            // price
            .ForMember(x => x.Costs, o => o.Ignore())
            .ForMember(x => x.Margin, o => o.Ignore())
            .ForMember(x => x.IsHidden, o => o.Ignore())
            .ForMember(x => x.Tags, o => o.Ignore())
            .ForMember(x => x.IsFavorite, o => o.Ignore())
            .ForMember(x => x.IsActive, o => o.Ignore())
            .ForMember(x => x.KeyDates, o => o.Ignore())
            .ForMember(x => x.Prices, o => o.Ignore())
            .ForMember(x => x.Properties, o => o.Ignore())

            // 
            // .ForMember(x => x.IsCustomPricing, o => o.Ignore())
            // .ForMember(x => x.Margin, o => o.Ignore())
            // .ForMember(x => x.Breadcrumbs, o => o.Ignore())
            ;
    }
}