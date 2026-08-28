using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HCS.CollaborationService.Data;

public static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    public static bool IsInboxDuplicate(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_CollaborationInbox"
        };
}
