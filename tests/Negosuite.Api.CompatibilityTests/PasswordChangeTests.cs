using negosuite_api.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Controllers;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class PasswordChangeTests
{
    [MySqlFact]
    public async Task Change_is_bound_to_active_actor_validates_password_and_preserves_other_accounts()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var actor = await f.Db.Users.SingleAsync();
        actor.Email = "actor@example.test";
        actor.Password = PasswordSecurity.Hash("OldPass123!");
        var other = new User { Name = "Other", Email = "other@example.test", Status = true, ConfigId = f.Company.Id, Password = PasswordSecurity.Hash("OtherPass123!") };
        f.Db.Users.Add(other); await f.Db.SaveChangesAsync();
        const string path = "/api/auth/change-password";
        Task<HttpResponseMessage> Change(string email, string password, string next) => f.Host.Client.PostAsJsonAsync(path, new { email, password, newPassword = next });
        Assert.Equal(HttpStatusCode.Forbidden, (await Change(other.Email, "OtherPass123!", "Changed123!")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Change(actor.Email, "Incorrect123!", "Changed123!")).StatusCode);
        foreach (var weak in new[] { "short1!", "longletters!", "123456789!", "letters123", "OldPass123!", new string('a', 249) + "1!" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Change(actor.Email, "OldPass123!", weak)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Change(actor.Email.ToUpperInvariant(), "OldPass123!", "Changed123!")).StatusCode);
        Assert.Equal(PasswordSecurity.Hash("Changed123!"), await f.Db.Users.AsNoTracking().Where(u => u.Id == actor.Id).Select(u => u.Password).SingleAsync());
        Assert.Equal(PasswordSecurity.Hash("OtherPass123!"), await f.Db.Users.AsNoTracking().Where(u => u.Id == other.Id).Select(u => u.Password).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Change(actor.Email, "OldPass123!", "Another123!")).StatusCode);
        // Do not let stale tracked values undo the password change.
        await f.Db.Entry(actor).ReloadAsync(); actor.Status = false; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Change(actor.Email, "Changed123!", "Another123!")).StatusCode);
        f.Host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token(userId: null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Change(actor.Email, "Changed123!", "Another123!")).StatusCode);
        f.Host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Change(actor.Email, "Changed123!", "Another123!")).StatusCode);
    }
}
