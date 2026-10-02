using System;
using System.Linq.Expressions;
using negosuite_api.Models;

namespace negosuite_api.Contracts.ReferenceData;

public static class ReferenceDataMapping
{
    public static readonly Expression<Func<PaymentMode, PaymentModeDetailDto>> PaymentModeProjection = e => new()
    {
        Id = e.Id,
        Name = e.Name,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        LastUpdatedDate = e.LastUpdatedDate,
        CreatedByUserId = e.CreatedByUserId,
        LastUpdatedByUserId = e.LastUpdatedByUserId
    };
    public static void Apply(PaymentModeWriteRequest input, PaymentMode entity)
    {
        entity.Name = input.Name;
        entity.IsActive = input.IsActive;
    }
    public static readonly Expression<Func<PaymentTerm, PaymentTermDetailDto>> PaymentTermProjection = e => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Days = e.Days,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        LastUpdatedDate = e.LastUpdatedDate,
        CreatedByUserId = e.CreatedByUserId,
        LastUpdatedByUserId = e.LastUpdatedByUserId
    };
    public static void Apply(PaymentTermWriteRequest input, PaymentTerm entity)
    {
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.Days = input.Days;
        entity.IsActive = input.IsActive;
    }
    public static readonly Expression<Func<Currency, CurrencyDetailDto>> CurrencyProjection = e => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        ExchangeRate = e.ExchangeRate,
        IsBase = e.IsBase,
        AltCode = e.AltCode
    };
    public static void Apply(CurrencyWriteRequest input, Currency entity)
    {
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.ExchangeRate = input.ExchangeRate;
        entity.IsBase = input.IsBase;
        entity.AltCode = input.AltCode;
    }
    public static readonly Expression<Func<Country, CountryDetailDto>> CountryProjection = e => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        CreatedDate = e.CreatedDate,
        LastUpdatedDate = e.LastUpdatedDate,
        CreatedByUserId = e.CreatedByUserId,
        LastUpdatedByUserId = e.LastUpdatedByUserId
    };
    public static void Apply(CountryWriteRequest input, Country entity)
    {
        entity.Code = input.Code;
        entity.Name = input.Name;
    }
    public static readonly Expression<Func<CityMunicipality, CityMunicipalityDetailDto>> CityMunicipalityProjection = e => new()
    {
        Id = e.Id,
        Name = e.Name,
        StateProvinceId = e.StateProvinceId,
        PostalCode = e.PostalCode,
        CreatedDate = e.CreatedDate,
        LastUpdatedDate = e.LastUpdatedDate,
        CreatedByUserId = e.CreatedByUserId,
        LastUpdatedByUserId = e.LastUpdatedByUserId,
        StateProvince = new ReferenceStateProvinceDto { Id = e.StateProvince.Id, Name = e.StateProvince.Name, Capital = e.StateProvince.Capital, CountryId = e.StateProvince.CountryId, CreatedDate = e.StateProvince.CreatedDate, LastUpdatedDate = e.StateProvince.LastUpdatedDate, CreatedByUserId = e.StateProvince.CreatedByUserId, LastUpdatedByUserId = e.StateProvince.LastUpdatedByUserId }
    };
    public static void Apply(CityMunicipalityWriteRequest input, CityMunicipality entity)
    {
        entity.Name = input.Name;
        entity.StateProvinceId = input.StateProvinceId;
        entity.PostalCode = input.PostalCode;
    }
    public static readonly Expression<Func<Industry, IndustryDetailDto>> IndustryProjection = e => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreatedDate = e.CreatedDate,
        LastUpdatedDate = e.LastUpdatedDate,
        CreatedByUserId = e.CreatedByUserId,
        LastUpdatedByUserId = e.LastUpdatedByUserId
    };
    public static void Apply(IndustryWriteRequest input, Industry entity)
    {
        entity.Name = input.Name;
    }
    public static readonly Expression<Func<NavigationItem, NavigationItemDetailDto>> NavigationItemProjection = e => new()
    {
        Id = e.Id,
        Title = e.Title,
        Subtitle = e.Subtitle,
        Type = e.Type,
        Icon = e.Icon,
        Link = e.Link,
        ParentId = e.ParentId
    };
    public static void Apply(NavigationItemWriteRequest input, NavigationItem entity)
    {
        entity.Title = input.Title;
        entity.Subtitle = input.Subtitle;
        entity.Type = input.Type;
        entity.Icon = input.Icon;
        entity.Link = input.Link;
        entity.ParentId = input.ParentId;
    }
}
