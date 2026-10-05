using System;
using System.Collections.Generic;
using negosuite_api.Models;

namespace negosuite_api.Services;

// The legacy documents share these named scalar fields without a common base model.
// Keep compatibility metadata here; transaction-specific fields remain in their own DTOs/services.
internal static class TransactionDocument
{
    internal static object Get(object value, string property) => value?.GetType().GetProperty(property)?.GetValue(value);
    internal static void Set(object value, string property, object data) => value?.GetType().GetProperty(property)?.SetValue(value, data);
    internal static IEnumerable<JournalEntry> Journals(object value) => Get(value, "JournalEntries") as IEnumerable<JournalEntry> ?? Array.Empty<JournalEntry>();
}
