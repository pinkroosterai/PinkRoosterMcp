using PinkRooster.Data.Entities;

namespace PinkRooster.Api.Services;

/// <summary>
/// Shared audit log helper methods used by all entity services.
/// The <c>factory</c> parameter creates an entry with entity-specific FK, ChangedBy, and ChangedAt already set.
/// </summary>
public static class AuditHelper
{
    public static void AddCreateEntry<TAudit>(
        List<TAudit> entries, Func<TAudit> factory,
        string field, string? value)
        where TAudit : IAuditLogEntry
    {
        if (value is null) return;
        var entry = factory();
        entry.FieldName = field;
        entry.NewValue = value;
        entries.Add(entry);
    }

    public static void AuditAndSet<TAudit>(
        List<TAudit> entries, Func<TAudit> factory,
        string field, string? oldValue, string newValue, Action<string> setter)
        where TAudit : IAuditLogEntry
    {
        if (oldValue == newValue) return;
        var entry = factory();
        entry.FieldName = field;
        entry.OldValue = oldValue;
        entry.NewValue = newValue;
        entries.Add(entry);
        setter(newValue);
    }

    public static void AuditAndSetEnum<TAudit, TEnum>(
        List<TAudit> entries, Func<TAudit> factory,
        string field, TEnum oldValue, TEnum newValue, Action<TEnum> setter)
        where TAudit : IAuditLogEntry
        where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(oldValue, newValue)) return;
        var entry = factory();
        entry.FieldName = field;
        entry.OldValue = oldValue.ToString();
        entry.NewValue = newValue.ToString();
        entries.Add(entry);
        setter(newValue);
    }

    public static void AuditAndSetValue<TAudit, T>(
        List<TAudit> entries, Func<TAudit> factory,
        string field, T oldValue, T newValue, Action<T> setter)
        where TAudit : IAuditLogEntry
        where T : IEquatable<T>
    {
        if (oldValue.Equals(newValue)) return;
        var entry = factory();
        entry.FieldName = field;
        entry.OldValue = oldValue.ToString();
        entry.NewValue = newValue.ToString();
        entries.Add(entry);
        setter(newValue);
    }

    public static void AuditAndSetNullable<TAudit, T>(
        List<TAudit> entries, Func<TAudit> factory,
        string field, T? oldValue, T? newValue, Action<T?> setter)
        where TAudit : IAuditLogEntry
        where T : struct
    {
        if (EqualityComparer<T?>.Default.Equals(oldValue, newValue)) return;
        var entry = factory();
        entry.FieldName = field;
        entry.OldValue = oldValue?.ToString();
        entry.NewValue = newValue?.ToString();
        entries.Add(entry);
        setter(newValue);
    }
}
