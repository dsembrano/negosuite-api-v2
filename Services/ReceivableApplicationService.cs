using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;
// Caller owns the transaction. Both Customer Payments and Sales Returns use these same AR links.
public sealed class ReceivableApplicationService
{
    private readonly negosuiteContext db;
    public ReceivableApplicationService(negosuiteContext db)=>this.db=db;
    public async Task LockAsync(int company,IEnumerable<int> ids,CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction!=null,"Credit application requires a transaction.");
        foreach(var id in ids.Distinct().OrderBy(x=>x))
        {
            var rows=await db.JournalEntries.FromSqlInterpolated($"SELECT * FROM journalentry WHERE Id={id} AND UserConfigId={company} FOR UPDATE").ToListAsync(ct);
            Require(rows.Count==1,"Invoice journal entry not found for this company.");
            await db.Entry(rows[0]).ReloadAsync(ct);
        }
    }
    public async Task ApplyAsync(int company,int customer,int account,int targetId,decimal amount,CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction!=null,"Credit application requires a transaction.");
        var target=await db.JournalEntries.SingleOrDefaultAsync(j=>j.Id==targetId&&j.UserConfigId==company,ct);
        Require(target!=null&&(target.CustomerId==customer||(target.CustomerId==null&&target.Source=="SI"&&target.SalesInvoiceId.HasValue&&await db.SalesInvoices.AnyAsync(i=>i.Id==target.SalesInvoiceId&&i.UserConfigId==company&&i.CustomerId==customer&&i.Status==1,ct)))&&target.AccountId==account&&target.Nature=="D"&&target.Status==1,"Select a posted receivable for this customer and account.");
        Require(amount==Math.Round(amount,2),"Applied credit must have at most two decimal places.");
        Require(target.Balance-amount>=0&&target.Balance-amount<=target.Amount,"Credit application exceeds the available invoice balance. Reload and review the allocation.",409);
        target.Balance-=amount;target.LastUpdatedDate=DateTime.UtcNow;
        if(target.Source=="SI")
        {
            var rows=await db.SalesInvoices.FromSqlInterpolated($"SELECT * FROM salesinvoice WHERE Id={target.SalesInvoiceId} AND UserConfigId={company} FOR UPDATE").ToListAsync(ct);
            var invoice=rows.SingleOrDefault();Require(invoice!=null&&invoice.CustomerId==customer&&invoice.Status==1,"The invoice is no longer posted or available.");
            invoice.Balance=target.Balance;invoice.LastUpdatedDate=DateTime.UtcNow;
        }
    }
}
