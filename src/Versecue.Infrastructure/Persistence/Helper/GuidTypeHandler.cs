using System;
using System.Data;
using Dapper;

namespace Versecue.Infrastructure.Persistence.Helper;

public sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
    {
        if (value is Guid guid)
            return guid;

        if (value is string text &&
            Guid.TryParse(text, out var parsed))
        {
            return parsed;
        }

        throw new DataException(
            $"Unable to convert value '{value}' to Guid.");
    }

    public override void SetValue(
        IDbDataParameter parameter,
        Guid value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }
}