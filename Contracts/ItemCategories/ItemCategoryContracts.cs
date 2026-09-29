using System;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.ItemCategories;

public class ItemCategoryListCriteria
{
    public int? UserConfigId { get; set; }
}

public class ItemCategoryWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    public bool Status { get; set; }
}

public sealed class ItemCategoryCreateRequest : ItemCategoryWriteRequest { }
public sealed class ItemCategoryUpdateRequest : ItemCategoryWriteRequest { }

// Preserve the eight fields returned by both legacy list and detail routes.
public class ItemCategoryDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}
