using Microsoft.Data.Sqlite;

namespace Versecue.Infrastructure.Persistence.Dapper;

/// <summary>
/// Factory for creating database connections.
/// Allows swapping between SQLite (MVP) and PostgreSQL (future) without changing repository code.
/// </summary>
public interface IDbConnectionFactory
{
    SqliteConnection CreateConnection();
}