using System.Globalization;
using System.Text;

namespace SmartSales.Services;

public static class Csv
{
    public static byte[] Build(IEnumerable<string> headers, IEnumerable<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(v => Escape(v switch
            {
                null => "",
                DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => v.ToString() ?? ""
            }))));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(string value)
    {
        // Prevent CSV/formula injection in spreadsheet apps.
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
