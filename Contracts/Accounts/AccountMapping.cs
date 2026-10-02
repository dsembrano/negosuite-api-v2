using negosuite_api.Models;

namespace negosuite_api.Contracts.Accounts;

public static class AccountMapping
{
    public static AccountCategoryDetailDto ToDto(AccountCategory value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Type = value.Type,
        OrderNo = value.OrderNo,
        AccountCodePrefix = value.AccountCodePrefix,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };

    public static void Apply(AccountCategoryWriteRequest input, AccountCategory value)
    {
        value.Name = input.Name;
        value.Type = input.Type;
        value.OrderNo = input.OrderNo;
        value.AccountCodePrefix = input.AccountCodePrefix;
    }

    public static AccountDetailDto ToDto(Account value, bool includeRelated = true) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        CategoryId = value.CategoryId,
        IsSubAccount = value.IsSubAccount,
        ParentAccountId = value.ParentAccountId,
        RequireCustomer = value.RequireCustomer,
        RequireSupplier = value.RequireSupplier,
        Notes = value.Notes,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        Category = includeRelated ? ToDto(value.Category) : null,
        ParentAccount = includeRelated ? ToDto(value.ParentAccount, false) : null
    };

    public static void Apply(AccountWriteRequest input, Account value)
    {
        value.Code = input.Code;
        value.Name = input.Name;
        value.CategoryId = input.CategoryId;
        value.IsSubAccount = input.IsSubAccount;
        value.ParentAccountId = input.ParentAccountId;
        value.RequireCustomer = input.RequireCustomer;
        value.RequireSupplier = input.RequireSupplier;
        value.Notes = input.Notes;
    }

}
