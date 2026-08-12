using Npgsql;
using Npgsql.NameTranslation;

namespace AgriDrone.Database;

internal sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    private static readonly NpgsqlSnakeCaseNameTranslator SnakeCaseTranslator = new();

    public static UpperSnakeCaseNameTranslator Instance { get; } = new();

    private UpperSnakeCaseNameTranslator()
    {
    }

    public string TranslateTypeName(string clrName) => Translate(clrName);

    public string TranslateMemberName(string clrName) => Translate(clrName);

    private static string Translate(string clrName) =>
        SnakeCaseTranslator.TranslateMemberName(clrName).ToUpperInvariant();
}
