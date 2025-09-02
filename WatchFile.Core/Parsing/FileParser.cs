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
        /// 解析CSV文件
        /// </summary>
        public static FileParseResult ParseCsv(string filePath, FileSettings settings)
        {
            var result = new FileParseResult();
            
            try
            {
                var encoding = GetEncoding(settings.Encoding);
                
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

        private static Dictionary<string, object> ExtractDataFromExcelRow(IRow row, FileSettings settings, IRow? headerRow)
        {
            var record = new Dictionary<string, object>();

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
            if (value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return mapping.Required ? throw new ArgumentException($"必需字段 '{mapping.TargetName}' 不能为空") : null;
            }

            var stringValue = value.ToString()!.Trim();

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
                    "UNICODE" => Encoding.Unicode,
                    _ => Encoding.UTF8
                };
            }
            catch (ArgumentException)
            {
                // 如果指定的编码不可用，返回 UTF-8 作为后备
                return Encoding.UTF8;
            }
        }
    }
}
