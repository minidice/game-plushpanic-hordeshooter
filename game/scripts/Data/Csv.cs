using System.Collections.Generic;
using System.Text;

namespace HordeShooter;

/// <summary>
/// Minimal spreadsheet-friendly CSV reader: UTF-8 (BOM ok), first row is the header, rows whose first
/// cell starts with '#' are comments (used for a human-readable description row), quotes supported.
/// </summary>
public static class Csv
{
    public sealed record Row(int Line, Dictionary<string, string> Cells);

    public static (List<string> header, List<Row> rows) Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var records = Records(text);
        var header = new List<string>();
        var rows = new List<Row>();
        foreach (var (line, cells) in records)
        {
            if (cells.Count == 0 || (cells.Count == 1 && cells[0].Trim() == "")) continue;
            if (cells[0].TrimStart().StartsWith('#')) continue;
            if (header.Count == 0)
            {
                foreach (var c in cells) header.Add(c.Trim());
                continue;
            }
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < header.Count; i++) dict[header[i]] = i < cells.Count ? cells[i].Trim() : "";
            rows.Add(new Row(line, dict));
        }
        return (header, rows);
    }

    private static List<(int line, List<string> cells)> Records(string text)
    {
        var result = new List<(int, List<string>)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        int line = 1, recordLine = 1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"') quoted = false;
                else
                {
                    if (c == '\n') line++;
                    cell.Append(c);
                }
                continue;
            }
            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    result.Add((recordLine, cells));
                    cells = new List<string>();
                    line++;
                    recordLine = line;
                    break;
                default:
                    cell.Append(c);
                    break;
            }
        }
        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            result.Add((recordLine, cells));
        }
        return result;
    }
}
