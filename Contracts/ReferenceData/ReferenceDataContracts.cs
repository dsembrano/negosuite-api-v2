using System;
using System.Collections.Generic;

namespace negosuite_api.Contracts.ReferenceData;

public abstract class ReferenceWriteRequest<TKey> where TKey : struct
{
    public TKey Id { get; set; }
}
public sealed class ReferenceDataFilter
{
    public bool ShowInactive { get; set; }
    public int? StateProvinceId { get; set; }
    public int? CountryId { get; set; }
}

public class PaymentModeWriteRequest : ReferenceWriteRequest<int>
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
public sealed class PaymentModeCreateRequest : PaymentModeWriteRequest { }
public sealed class PaymentModeUpdateRequest : PaymentModeWriteRequest { }

public sealed class PaymentModeDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class PaymentTermWriteRequest : ReferenceWriteRequest<int>
{
    public string Code { get; set; }
    public string Name { get; set; }
    public short? Days { get; set; }
    public bool IsActive { get; set; }
}
public sealed class PaymentTermCreateRequest : PaymentTermWriteRequest { }
public sealed class PaymentTermUpdateRequest : PaymentTermWriteRequest { }

public sealed class PaymentTermDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public short? Days { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class CurrencyWriteRequest : ReferenceWriteRequest<short>
{
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsBase { get; set; }
    public string AltCode { get; set; }
}
public sealed class CurrencyCreateRequest : CurrencyWriteRequest { }
public sealed class CurrencyUpdateRequest : CurrencyWriteRequest { }

public sealed class CurrencyDetailDto
{
    public short Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsBase { get; set; }
    public string AltCode { get; set; }

}

public class CountryWriteRequest : ReferenceWriteRequest<int>
{
    public string Code { get; set; }
    public string Name { get; set; }
}
public sealed class CountryCreateRequest : CountryWriteRequest { }
public sealed class CountryUpdateRequest : CountryWriteRequest { }

public sealed class CountryDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public List<ReferenceStateProvinceDto> StateProvinces { get; set; } = new();
}

public class CityMunicipalityWriteRequest : ReferenceWriteRequest<int>
{
    public string Name { get; set; }
    public int StateProvinceId { get; set; }
    public string PostalCode { get; set; }
}
public sealed class CityMunicipalityCreateRequest : CityMunicipalityWriteRequest { }
public sealed class CityMunicipalityUpdateRequest : CityMunicipalityWriteRequest { }

public sealed class CityMunicipalityDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateProvinceId { get; set; }
    public string PostalCode { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ReferenceStateProvinceDto StateProvince { get; set; }
}

public class IndustryWriteRequest : ReferenceWriteRequest<int>
{
    public string Name { get; set; }
}
public sealed class IndustryCreateRequest : IndustryWriteRequest { }
public sealed class IndustryUpdateRequest : IndustryWriteRequest { }

public sealed class IndustryDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class NavigationItemWriteRequest : ReferenceWriteRequest<int>
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Type { get; set; }
    public string Icon { get; set; }
    public string Link { get; set; }
    public int? ParentId { get; set; }
}
public sealed class NavigationItemCreateRequest : NavigationItemWriteRequest { }
public sealed class NavigationItemUpdateRequest : NavigationItemWriteRequest { }

public sealed class NavigationItemDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Type { get; set; }
    public string Icon { get; set; }
    public string Link { get; set; }
    public int? ParentId { get; set; }
    public NavigationItemDetailDto Parent { get; set; }
    public List<NavigationItemDetailDto> Children { get; set; }
}

public sealed class ReferenceStateProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Capital { get; set; }
    public int CountryId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public CountryDetailDto Country { get; set; }
}
public sealed class CityMunicipalityListDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateProvinceId { get; set; }
    public string StateProvinceName { get; set; }
    public string SelectOptionName { get; set; }
    public string PostalCode { get; set; }
}
public sealed class IndustryListDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}
public class NavigationChildDto
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Type { get; set; }
    public string Link { get; set; }
    public string Icon { get; set; }
}
public sealed class NavigationRootDto : NavigationChildDto
{
    public List<NavigationChildDto> Children { get; set; }
}
