using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using negosuite_api.Models;

namespace Negosuite.Api.CompatibilityTests;

// CRUD/rounding fixtures need a real balancing side now that posting validates equality.
// This helper is used explicitly by success-path fixtures, never by rejection probes.
internal static class FixtureJournals
{
    public const string Counter = "Fixture balancing entry";
    public static T Balance<T>(T document) where T : class
    {
        var property = document.GetType().GetProperty("JournalEntries");
        if (property == null) return document;
        var entries = (ICollection<JournalEntry>)property.GetValue(document)!;
        var active = entries.Where(j => j.Deleted != true && j.Notes != Counter).ToArray();
        var counter = entries.SingleOrDefault(j => j.Notes == Counter);
        if (active.Length == 0) { if (counter != null) counter.Deleted = true; return document; }
        var delta = active.Sum(j => j.Nature == "D" ? j.Amount : -j.Amount);
        if (counter == null) { counter = new JournalEntry(); entries.Add(counter); }
        var source = active[0];
        counter.UserConfigId = source.UserConfigId; counter.AccountId = source.AccountId;
        counter.ReferenceNo = source.ReferenceNo; counter.JournalDate = source.JournalDate;
        counter.Source = source.Source; counter.Notes = Counter; counter.Status = source.Status;
        counter.Nature = delta >= 0 ? "C" : "D"; counter.Amount = counter.Balance = System.Math.Abs(delta);
        return document;
    }
    public static JsonNode Balance(JsonNode document)
    {
        if (document["journalEntries"] is not JsonArray entries) return document;
        var active = entries.Where(j => j?["deleted"]?.GetValue<bool>() != true && j?["notes"]?.GetValue<string>() != Counter).ToArray();
        if (active.Length == 0) return document;
        var counter = entries.SingleOrDefault(j => j?["notes"]?.GetValue<string>() == Counter);
        if (counter == null)
        {
            counter = active[0]!.DeepClone(); counter["id"] = 0; counter["notes"] = Counter;
            counter["paymentToJournalEntryId"] = null; entries.Add(counter);
        }
        var delta = active.Sum(j => (j!["nature"]!.GetValue<string>() == "D" ? 1 : -1) * j["amount"]!.Deserialize<decimal>());
        counter["nature"] = delta >= 0 ? "C" : "D";
        counter["amount"] = System.Math.Abs(delta); counter["balance"] = System.Math.Abs(delta);
        return document;
    }
    public static IEnumerable<JournalEntry> Business(IEnumerable<JournalEntry> entries) => entries.Where(j => j.Notes != Counter);
    public static IEnumerable<JsonElement> Business(JsonElement entries) => entries.EnumerateArray().Where(j => !j.TryGetProperty("notes", out var note) || note.GetString() != Counter);
}
