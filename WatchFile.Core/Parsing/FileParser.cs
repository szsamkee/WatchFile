using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using WatchFile.Core.Configuration.Models;
using WatchFile.Core.Events;
using CsvHelper;
using CsvHelper.Configuration;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace WatchFile.Core.Parsing
{
    /// <summary>
    /// 文件解析器
    /// </summary>
    public static class FileParser
    {
        /// <summary>
        /// 解析文件内容
        /// </summary>
        public static FileParseResult ParseFile(string filePath, FileSettings settings)
        {
            try
            {
                // 简单模式：只检查文件是否存在，不解析内容
                if (settings.SimpleMode)
                {
                    return ParseSimpleMode(filePath, settings);
                }

                return settings.FileType switch
                {
                    FileType.CSV => ParseCsv(filePath, settings),
                    FileType.Excel => ParseExcel(filePath, settings),
                    _ => new FileParseResult { Exception = new NotSupportedException($"不支持的文件类型: {settings.FileType}") }
                };
            }
            catch (Exception ex)
            {
                return new FileParseResult { Exception = ex };
            }
        }

        /// <summary>
        /// 简单模式解析：不解析文件内容，只返回文件基本信息
        /// </summary>
        private static FileParseResult ParseSimpleMode(string filePath, FileSettings settings)
        {
            var result = new FileParseResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Exception = new FileNotFoundException($"文件不存在: {filePath}");
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                var simpleRecord = new Dictionary<string, object>
                {
                    ["FileName"] = fileInfo.Name,
                    ["FilePath"] = filePath,
                    ["FileSize"] = fileInfo.Length,
                    ["LastWriteTime"] = fileInfo.LastWriteTime,
                    ["CreationTime"] = fileInfo.CreationTime
                };

                result.Data = new List<Dictionary<string, object>> { simpleRecord };
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        /// <summary>
        /// 解析CSV文件
        /// </summary>
        public static FileParseResult ParseCsv(string filePath, FileSettings settings)
        {
            var result = new FileParseResult();
            
            try
            {
                // 🔍 尝试自动检测编码
                var detectedEncoding = DetectFileEncoding(filePath);
                var encoding = detectedEncoding ?? GetEncoding(settings.Encoding);
                
                using var reader = new StreamReader(filePath, encoding);
                using var csv = new CsvReader(reader, CreateCsvConfiguration(settings));

                var records = new List<Dictionary<string, object>>();

                if (settings.HasHeader)
                {
                    // 有标题行的处理
                    csv.Read();
                    csv.ReadHeader();
                    
                    while (csv.Read())
                    {
                        var record = ExtractDataFromCsvRecord(csv, settings, true);
                        if (record.Count > 0)
                        {
                            records.Add(record);
                        }
                    }
                }
                else
                {
                    // 无标题行的处理
                    while (csv.Read())
                    {
                        var record = ExtractDataFromCsvRecord(csv, settings, false);
                        if (record.Count > 0)
                        {
                            records.Add(record);
                        }
                    }
                }

                result.Data = records;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        /// <summary>
        /// 解析Excel文件
        /// </summary>
        public static FileParseResult ParseExcel(string filePath, FileSettings settings)
        {
            var result = new FileParseResult();
            
            try
            {
                IWorkbook? workbook = null;
                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    workbook = extension switch
                    {
                        ".xls" => new HSSFWorkbook(fileStream),
                        ".xlsx" => new XSSFWorkbook(fileStream),
                        _ => throw new NotSupportedException($"不支持的Excel文件格式: {extension}")
                    };

                    var sheet = workbook.GetSheet(settings.SheetName) ?? workbook.GetSheetAt(0);
                    if (sheet == null)
                    {
                        throw new InvalidOperationException($"找不到工作表: {settings.SheetName}");
                    }

                    var records = new List<Dictionary<string, object>>();
                    var startRow = settings.StartRow - 1; // NPOI使用0基索引
                    
                    // 如果有标题行，需要先读取标题
                    var headerRow = settings.HasHeader ? sheet.GetRow(startRow) : null;
                    var dataStartRow = settings.HasHeader ? startRow + 1 : startRow;

                    for (int rowIndex = dataStartRow; rowIndex <= sheet.LastRowNum; rowIndex++)
                    {
                        var row = sheet.GetRow(rowIndex);
                        if (row == null) continue;

                        var record = ExtractDataFromExcelRow(row, settings, headerRow);
                        if (record.Count > 0)
                        {
                            records.Add(record);
                        }
                    }

                    result.Data = records;
                    result.IsSuccess = true;
                }
                finally
                {
                    // 🔧 修复：确保 workbook 对象被正确释放
                    workbook?.Close();
                }
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        private static CsvConfiguration CreateCsvConfiguration(FileSettings settings)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = settings.Delimiter,
                HasHeaderRecord = settings.HasHeader,
                BadDataFound = null, // 忽略错误数据
                MissingFieldFound = null // 忽略缺失字段
            };

            return config;
        }

        private static Dictionary<string, object> ExtractDataFromCsvRecord(CsvReader csv, FileSettings settings, bool hasHeader)
        {
            var record = new Dictionary<string, object>();

            // 如果没有列映射配置，返回所有列的原始数据
            if (settings.ColumnMappings == null || !settings.ColumnMappings.Any())
            {
                if (hasHeader)
                {
                    // 有标题的情况：使用列名作为键
                    for (int i = 0; i < csv.HeaderRecord?.Length; i++)
                    {
                        var columnName = csv.HeaderRecord[i];
                        var value = csv.GetField(i) ?? string.Empty;
                        record[columnName] = value;
                    }
                }
                else
                {
                    // 无标题的情况：使用Column_N作为键
                    var recordLength = csv.Parser.Count;
                    for (int i = 0; i < recordLength; i++)
                    {
                        var value = csv.GetField(i) ?? string.Empty;
                        record[$"Column_{i}"] = value;
                    }
                }
                return record;
            }

            // 原有的列映射逻辑
            foreach (var mapping in settings.ColumnMappings)
            {
                try
                {
                    object value;
                    
                    // sourceColumnSeq 优先于 sourceColumn
                    if (mapping.SourceColumnSeq.HasValue)
                    {
                        // 使用列索引
                        value = csv.GetField(mapping.SourceColumnSeq.Value) ?? string.Empty;
                    }
                    else if (hasHeader && !string.IsNullOrEmpty(mapping.SourceColumn))
                    {
                        // 使用列名
                        value = csv.GetField(mapping.SourceColumn) ?? string.Empty;
                    }
                    else
                    {
                        continue;
                    }

                    var convertedValue = ConvertValue(value, mapping);
                    
                    // 🔧 修复：即使值为null，也应该添加字段到记录中
                    // 对于Required字段，如果值为null则使用类型默认值
                    if (convertedValue == null && mapping.Required)
                    {
                        // Required字段为null时，根据数据类型提供默认值
                        convertedValue = GetDefaultValueForType(mapping.DataType);
                    }
                    
                    record[mapping.TargetName] = convertedValue ?? DBNull.Value;
                }
                catch (Exception ex)
                {
                    if (mapping.Required)
                    {
                        throw new InvalidOperationException($"处理必需列 '{mapping.TargetName}' 时出错: {ex.Message}", ex);
                    }
                    // 非必需列出错时继续处理
                }
            }

            return record;
        }

        private static Dictionary<string, object> ExtractDataFromExcelRow(IRow row, FileSettings settings, IRow? headerRow)
        {
            var record = new Dictionary<string, object>();

            // 如果没有列映射配置，返回所有列的原始数据
            if (settings.ColumnMappings == null || !settings.ColumnMappings.Any())
            {
                for (int i = 0; i < row.LastCellNum; i++)
                {
                    var cell = row.GetCell(i);
                    var value = GetCellValue(cell);
                    
                    if (headerRow != null && settings.HasHeader)
                    {
                        // 有标题的情况：使用列名作为键
                        var headerCell = headerRow.GetCell(i);
                        var columnName = headerCell?.ToString()?.Trim() ?? $"Column_{i}";
                        record[columnName] = value ?? string.Empty;
                    }
                    else
                    {
                        // 无标题的情况：使用Column_N作为键
                        record[$"Column_{i}"] = value ?? string.Empty;
                    }
                }
                return record;
            }

            // 原有的列映射逻辑
            foreach (var mapping in settings.ColumnMappings)
            {
                try
                {
                    ICell? cell = null;
                    
                    // sourceColumnSeq 优先于 sourceColumn
                    if (mapping.SourceColumnSeq.HasValue)
                    {
                        // 使用列索引
                        cell = row.GetCell(mapping.SourceColumnSeq.Value);
                    }
                    else if (settings.HasHeader && headerRow != null && !string.IsNullOrEmpty(mapping.SourceColumn))
                    {
                        // 根据列名查找列索引
                        var columnIndex = FindColumnIndexByName(headerRow, mapping.SourceColumn);
                        if (columnIndex >= 0)
                        {
                            cell = row.GetCell(columnIndex);
                        }
                    }

                    var value = GetCellValue(cell);
                    var convertedValue = ConvertValue(value, mapping);
                    
                    if (convertedValue != null || !mapping.Required)
                    {
                        record[mapping.TargetName] = convertedValue ?? DBNull.Value;
                    }
                }
                catch (Exception ex)
                {
                    if (mapping.Required)
                    {
                        throw new InvalidOperationException($"处理必需列 '{mapping.TargetName}' 时出错: {ex.Message}", ex);
                    }
                    // 非必需列出错时继续处理
                }
            }

            return record;
        }

        private static int FindColumnIndexByName(IRow headerRow, string columnName)
        {
            for (int i = 0; i < headerRow.LastCellNum; i++)
            {
                var cell = headerRow.GetCell(i);
                if (cell != null && cell.ToString()?.Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return i;
                }
            }
            return -1;
        }

        private static object? GetCellValue(ICell? cell)
        {
            if (cell == null) return null;

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue : cell.NumericCellValue,
                CellType.Boolean => cell.BooleanCellValue,
                CellType.Formula => cell.CachedFormulaResultType switch
                {
                    CellType.String => cell.StringCellValue,
                    CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue : cell.NumericCellValue,
                    CellType.Boolean => cell.BooleanCellValue,
                    _ => cell.ToString()
                },
                _ => cell.ToString()
            };
        }

        private static object? ConvertValue(object? value, ColumnMapping mapping)
        {
            // 🔧 修复：正确处理空值字段
            // 空值可能是：null, DBNull, 空字符串, 或只包含空白字符的字符串
            if (value == null || 
                value == DBNull.Value || 
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                // 如果是必需字段且为空，不抛出异常，而是返回null让上层处理
                // 上层会根据数据类型提供默认值
                if (mapping.Required)
                {
                    return null; // 让上层的ExtractDataFromCsvRecord处理默认值
                }
                return null; // 非必需字段直接返回null
            }

            var stringValue = value.ToString()!.Trim();
            
            // 再次检查trim后是否为空
            if (string.IsNullOrEmpty(stringValue))
            {
                return mapping.Required ? null : null;
            }

            try
            {
                return mapping.DataType switch
                {
                    DataType.String => stringValue,
                    DataType.Integer => Convert.ToInt32(stringValue),
                    DataType.Decimal => Convert.ToDecimal(stringValue),
                    DataType.Boolean => Convert.ToBoolean(stringValue),
                    DataType.DateTime => ParseDateTime(stringValue, mapping.Format),
                    _ => stringValue
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法将值 '{stringValue}' 转换为 {mapping.DataType} 类型", ex);
            }
        }

        private static DateTime ParseDateTime(string value, string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return DateTime.Parse(value);
            }

            return DateTime.ParseExact(value, format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 根据数据类型获取默认值
        /// </summary>
        private static object GetDefaultValueForType(DataType dataType)
        {
            return dataType switch
            {
                DataType.String => string.Empty,
                DataType.Integer => 0,
                DataType.Decimal => 0.0m,
                DataType.Boolean => false,
                DataType.DateTime => DateTime.MinValue,
                _ => string.Empty
            };
        }

        private static Encoding GetEncoding(string encodingName)
        {
            // 🔧 修复：自动注册编码提供程序以支持 GB2312、GBK 等编码
            // 这样使用者就不需要手动注册了
            try 
            {
                // 尝试注册编码提供程序（如果已经注册过，不会有副作用）
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            }
            catch 
            {
                // 静默忽略注册错误（可能已经注册过）
            }

            try
            {
                return encodingName.ToUpperInvariant() switch
                {
                    "UTF-8" or "UTF8" => Encoding.UTF8,
                    "GBK" => Encoding.GetEncoding("GBK"),
                    "GB2312" => Encoding.GetEncoding("GB2312"),
                    "ASCII" => Encoding.ASCII,
                    "UNICODE" => Encoding.Unicode,  // UTF-16 LE
                    "UTF-16" or "UTF16" => Encoding.Unicode,  // UTF-16 LE
                    "UTF-16BE" or "UTF16BE" => Encoding.BigEndianUnicode,  // UTF-16 BE
                    "UTF-32" or "UTF32" => Encoding.UTF32,
                    _ => Encoding.UTF8
                };
            }
            catch (ArgumentException)
            {
                // 如果指定的编码不可用，返回 UTF-8 作为后备
                return Encoding.UTF8;
            }
        }

        /// <summary>
        /// 自动检测文件编码（通过BOM和第一行内容）
        /// </summary>
        private static Encoding DetectFileEncoding(string filePath)
        {
            // 🔧 先注册编码提供程序，以支持 GBK 等编码
            try 
            {
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            }
            catch 
            {
                // 静默忽略注册错误（可能已经注册过）
            }
            
            // 读取文件前4个字节来检测BOM
            var bom = new byte[4];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // 检测BOM
            // UTF-8: EF BB BF
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return Encoding.UTF8;
            }
            
            // UTF-16 LE: FF FE
            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] != 0x00)
            {
                return Encoding.Unicode;
            }
            
            // UTF-16 BE: FE FF
            if (bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }
            
            // UTF-32 LE: FF FE 00 00
            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            {
                return Encoding.UTF32;
            }
            
            // UTF-32 BE: 00 00 FE FF
            if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
            {
                try
                {
                    return Encoding.GetEncoding("UTF-32BE");
                }
                catch
                {
                    return Encoding.UTF32;
                }
            }

            // 🔧 智能检测：通过第一行内容判断编码
            return DetectEncodingByFirstLine(filePath);
        }

        /// <summary>
        /// 通过第一行内容检测文件编码
        /// </summary>
        private static Encoding DetectEncodingByFirstLine(string filePath)
        {
            // 尝试不同编码读取第一行，看哪个能正确解析中文
            var encodingsToTry = new[]
            {
                new { Encoding = Encoding.GetEncoding("GBK"), Name = "GBK" },
                new { Encoding = Encoding.GetEncoding("GB2312"), Name = "GB2312" },
                new { Encoding = Encoding.UTF8, Name = "UTF-8" },
                new { Encoding = Encoding.Unicode, Name = "UTF-16LE" },
                new { Encoding = Encoding.BigEndianUnicode, Name = "UTF-16BE" },
                new { Encoding = Encoding.ASCII, Name = "ASCII" }
            };

            foreach (var encodingInfo in encodingsToTry)
            {
                try
                {
                    using var reader = new StreamReader(filePath, encodingInfo.Encoding);
                    var firstLine = reader.ReadLine();
                    
                    if (string.IsNullOrEmpty(firstLine))
                        continue;

                    // 检查是否包含中文字符，并且没有乱码
                    if (ContainsChineseCharacters(firstLine) && !ContainsGarbledText(firstLine))
                    {
                        return encodingInfo.Encoding;
                    }
                    
                    // 如果没有中文字符但也没有乱码，记录为候选编码
                    if (!ContainsGarbledText(firstLine))
                    {
                        // 对于纯ASCII内容，优先选择UTF-8
                        if (encodingInfo.Name == "UTF-8")
                        {
                            return encodingInfo.Encoding;
                        }
                    }
                }
                catch
                {
                    // 该编码无法读取，尝试下一个
                    continue;
                }
            }

            // 如果都无法确定，返回null使用配置的编码
            return null;
        }

        /// <summary>
        /// 检查字符串是否包含中文字符
        /// </summary>
        private static bool ContainsChineseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                // 中文字符的Unicode范围
                if ((c >= 0x4E00 && c <= 0x9FFF) ||  // CJK统一汉字
                    (c >= 0x3400 && c <= 0x4DBF) ||  // CJK扩展A
                    (c >= 0xF900 && c <= 0xFAFF))    // CJK兼容汉字
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查字符串是否包含乱码字符
        /// </summary>
        private static bool ContainsGarbledText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 检查是否包含常见的乱码字符
            var garbledCount = 0;
            foreach (char c in text)
            {
                // 替换字符 (常见的乱码字符)
                if (c == 0xFFFD || c == '�')
                {
                    garbledCount++;
                }
                // 连续的问号也可能是乱码
                if (c == '?' && text.Contains("???"))
                {
                    garbledCount++;
                }
            }

            // 如果乱码字符超过一定比例，认为是乱码
            return garbledCount > 0 && (double)garbledCount / text.Length > 0.1;
        }
    }
}
