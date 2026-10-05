using System;
using System.ComponentModel.DataAnnotations;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.ReferenceData;

namespace negosuite_api.Contracts.Auth;

public sealed class SignInRequest
{
    public string UserName { get; set; }
    public string Email { get; set; }
    [Required] public string Password { get; set; }
    [MaxLength(45)] public string Platform { get; set; }
    [MaxLength(20)] public string PlatformVersion { get; set; }
}
public sealed class RefreshAccessTokenRequest
{
    public string RefreshToken { get; set; }
}
public sealed class ChangePasswordRequest
{
    [Required] public string Email { get; set; }
    [Required] public string Password { get; set; }
    [Required] public string NewPassword { get; set; }
}
public sealed class ResetPasswordRequest
{
    [Required] public string Email { get; set; }
    [Required] public string Password { get; set; }
    [Required] public string Identifier { get; set; }
}
public class AuthSessionResponse
{
    public AuthUserDto User { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string TokenType { get; set; } = "bearer";
    public AuthAppVersionDto AppVersion { get; set; }
}
public sealed class SignInResponse : AuthSessionResponse
{
    public AuthUserLogDto UserLog { get; set; }
}
public sealed class AuthUserDto
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string MobileNo { get; set; }
    public int? UserTypeId { get; set; }
    public string UserTypeName { get; set; }
    public string Avatar { get; set; }
    public bool Status { get; set; }
    public int? ConfigId { get; set; }
    public ConfigDetailDto Config { get; set; }
    public CurrencyDetailDto BaseCurrency { get; set; }
    public int? UserRoleId { get; set; }
    public UserRoleDetailDto UserRole { get; set; }
    public string UserUIConfig { get; set; }
}
public sealed class AuthUserLogDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime SignInDate { get; set; }
    public string AccessToken { get; set; }
    public string Platform { get; set; }
    public string PlatformVersion { get; set; }
    public string RefreshToken { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool? IsRevoked { get; set; }
}
public sealed class AuthAppVersionDto
{
    public string LatestVersion { get; set; }
    public string AndroidUpdateUrl { get; set; }
    public int UpdateMode { get; set; } = 1;
}
public sealed class AppVersionDetailDto
{
    public int Id { get; set; }
    public string VersionCode { get; set; }
    public string AndroidUpdateUrl { get; set; }
    public string APKFilename { get; set; }
}
public sealed record MetabaseTokenResponse(string MetabaseToken);
