namespace HCS.WorkManagementService.Domain;

internal static class WorkTimestamps
{
    /// <summary>
    /// Npgsql timestamptz rejects Unspecified/Local DateTime. Date pickers bind Unspecified;
    /// treat that calendar value as UTC so the stored day does not shift.
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
