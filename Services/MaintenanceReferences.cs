using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using negosuite_api.Models;

namespace negosuite_api.Services;

// Explicitly check references even where the legacy schema has no foreign-key constraint.
public sealed class MaintenanceReferences
{
    private readonly negosuiteContext db;
    public MaintenanceReferences(negosuiteContext db) => this.db = db;
    public async Task<bool> UsedAsync(int id, string[] columns, string[] jsonColumns, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection(); var opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        try
        {
            foreach (var entity in db.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() != null && e.GetTableName() != null))
            {
                var table = entity.GetTableName(); var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
                var predicates = entity.GetProperties().Where(p => columns.Contains(p.Name) || jsonColumns.Contains(p.Name)).Select(p =>
                {
                    var column = "`" + p.GetColumnName(store).Replace("`", "``") + "`";
                    return jsonColumns.Contains(p.Name)
                        ? $"JSON_CONTAINS(IF(JSON_VALID({column}), {column}, JSON_ARRAY()), JSON_OBJECT('id', @id), '$')"
                        : column + " = @id";
                }).ToArray();
                if (predicates.Length == 0) continue;
                await using var command = connection.CreateCommand();
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                // Identifiers come only from EF metadata; record IDs are bound parameters.
                command.CommandText = "SELECT EXISTS(SELECT 1 FROM `" + table.Replace("`", "``") + "` WHERE " + string.Join(" OR ", predicates) + " LIMIT 1)";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = id; command.Parameters.Add(parameter);
                if (Convert.ToBoolean(await command.ExecuteScalarAsync(ct))) return true;
            }
            return false;
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }
}
