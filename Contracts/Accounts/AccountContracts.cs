using System;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.Accounts;

public sealed class AccountListCriteria
{
    public int? UserConfigId { get; set; }
    public int? CategoryId { get; set; }
}

public class AccountWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    [StringLength(20)] public string Code { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    public int CategoryId { get; set; }
    public bool IsSubAccount { get; set; }
    public int? ParentAccountId { get; set; }
    public bool RequireCustomer { get; set; }
    public bool RequireSupplier { get; set; }
    public string Notes { get; set; }
}
public sealed class AccountCreateRequest : AccountWriteRequest { }
public sealed class AccountUpdateRequest : AccountWriteRequest { }

public class AccountDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; }
    public bool IsSubAccount { get; set; }
    public int? ParentAccountId { get; set; }
    public bool RequireCustomer { get; set; }
    public bool RequireSupplier { get; set; }
    public string Notes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public AccountCategoryDetailDto Category { get; set; }
    public AccountDetailDto ParentAccount { get; set; }
}

public class AccountCategoryWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    [Required, StringLength(10)] public string Type { get; set; }
    public int OrderNo { get; set; }
    public string AccountCodePrefix { get; set; }
}
public sealed class AccountCategoryCreateRequest : AccountCategoryWriteRequest { }
public sealed class AccountCategoryUpdateRequest : AccountCategoryWriteRequest { }

public class AccountCategoryDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public int OrderNo { get; set; }
    public string AccountCodePrefix { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}

public sealed class AccountListDto
{
    public bool IsInventoryAccount { get; set; }
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; }
    public int? ParentAccountId { get; set; }
    public string ParentAccountCode { get; set; }
    public string ParentAccountName { get; set; }
    public bool RequireCustomer { get; set; }
    public bool RequireSupplier { get; set; }
    public string Type { get; set; }
    public string SortCode { get; set; }
}

public sealed class AccountCategoryListDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string AccountCodePrefix { get; set; }
    public int OrderNo { get; set; }
    public int AccountCount { get; set; }
}

public class AccountHeaderDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; }
    public int? ParentAccountId { get; set; }
    public string ParentAccountCode { get; set; }
    public AccountCategoryDetailDto Category { get; set; }
}
public sealed class AccountLinkDto : AccountHeaderDto
{
    public AccountDetailDto ParentAccount { get; set; }
}
