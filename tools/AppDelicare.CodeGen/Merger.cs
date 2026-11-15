using System.Text;
using System.Text.RegularExpressions;

namespace AppDelicare.CodeGen;

/// <summary>T45R: ghép thuộc tính còn thiếu từ bản sinh vào file dev sở hữu (Request / ViewModel cũ) – chỉ thêm, không sửa / xóa dòng cũ.</summary>
public static partial class Merger
{
    public static string AddMissingProperties(string existing, string generated)
    {
        var have = PropertyName().Matches(existing).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var lines = generated.Replace("\r\n", "\n").Split('\n');
        var block = new List<string>();
        var pending = new List<string>();
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                // Không thêm [Required] mới vào DTO cũ (đổi hành vi kiểm tra đầu vào); service quyết định bắt buộc. Giữ [MaxLength].
                if (!trimmed.StartsWith("[Required", StringComparison.Ordinal))
                {
                    pending.Add(trimmed);
                }
                continue;
            }
            var m = PropertyName().Match(line);
            if (m.Success && !have.Contains(m.Groups[1].Value))
            {
                block.AddRange(pending);
                block.Add(trimmed);
            }
            pending.Clear();
        }
        if (block.Count == 0)
        {
            return existing;
        }
        string text = existing.Replace("\r\n", "\n");
        // Chèn trước dấu đóng class (dấu "}" thứ hai từ cuối: class rồi namespace).
        int nsClose = text.TrimEnd().LastIndexOf('}');
        int classClose = text.LastIndexOf('}', nsClose - 1);
        int lineStart = text.LastIndexOf('\n', classClose) + 1;
        string indent = text[lineStart..classClose];
        string member = indent.Contains('\t') ? indent + "\t" : indent + "    ";
        var add = new StringBuilder();
        add.Append(member).Append("// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).").Append('\n');
        foreach (string l in block)
        {
            add.Append(member).Append(l).Append('\n');
        }
        string merged = text[..lineStart] + add + text[lineStart..];
        const string annotations = "using System.ComponentModel.DataAnnotations;";
        if (block.Any(l => l.StartsWith('[')) && !merged.Contains(annotations))
        {
            merged = annotations + "\n" + merged.TrimStart('﻿');
        }
        return merged;
    }

    [GeneratedRegex(@"public\s+[^\s=]+\s+(\w+)\s*\{\s*get;")]
    private static partial Regex PropertyName();
}
