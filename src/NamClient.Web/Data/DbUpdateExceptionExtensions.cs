using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NamClient.Web.Data;

public static class DbUpdateExceptionExtensions
{
    private const int DuplicateKeyRow = 2601;
    private const int DuplicateKeyConstraint = 2627;

    public static bool IsUniqueConstraintViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: DuplicateKeyRow or DuplicateKeyConstraint };
}
