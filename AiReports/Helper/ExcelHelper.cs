using ExcelDataReader;
using System;
using System.Data;
using System.IO;


namespace AiReports.Helper
{

    public static class ExcelHelper
    {

        public static DataTable ReadExcelFile(string filePath, int sheetIndex = 0, bool hasHeaderRow = true)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"فایل Excel یافت نشد: {filePath}");
            }

            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = CreateExcelReader(stream, filePath))
                    {
                        if (reader == null)
                        {
                            throw new Exception("فرمت فایل Excel پشتیبانی نمی‌شود");
                        }

                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = hasHeaderRow
                            }
                        });

                        if (result.Tables.Count == 0)
                        {
                            throw new Exception("فایل Excel هیچ sheet‌ای ندارد");
                        }

                        if (sheetIndex >= result.Tables.Count)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(sheetIndex),
                                $"شماره sheet معتبر نیست. تعداد sheet‌ها: {result.Tables.Count}");
                        }

                        return result.Tables[sheetIndex];
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در خواندن فایل Excel: {filePath}", ex);
            }
        }


        public static DataSet ReadAllSheets(string filePath, bool hasHeaderRow = true)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"فایل Excel یافت نشد: {filePath}");
            }

            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = CreateExcelReader(stream, filePath))
                    {
                        if (reader == null)
                        {
                            throw new Exception("فرمت فایل Excel پشتیبانی نمی‌شود");
                        }

                        return reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = hasHeaderRow
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در خواندن فایل Excel: {filePath}", ex);
            }
        }


        public static DataTable ReadSheetByName(string filePath, string sheetName, bool hasHeaderRow = true)
        {
            var dataSet = ReadAllSheets(filePath, hasHeaderRow);

            if (dataSet.Tables.Contains(sheetName))
            {
                return dataSet.Tables[sheetName];
            }

            throw new ArgumentException($"Sheet با نام '{sheetName}' یافت نشد");
        }


        public static string[] GetSheetNames(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"فایل Excel یافت نشد: {filePath}");
            }

            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = CreateExcelReader(stream, filePath))
                    {
                        if (reader == null)
                        {
                            throw new Exception("فرمت فایل Excel پشتیبانی نمی‌شود");
                        }

                        var sheetNames = new string[reader.ResultsCount];
                        for (int i = 0; i < reader.ResultsCount; i++)
                        {
                            sheetNames[i] = reader.Name;
                            reader.NextResult();
                        }

                        return sheetNames;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در خواندن نام sheet‌ها: {filePath}", ex);
            }
        }

        private static IExcelDataReader CreateExcelReader(Stream stream, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".xlsx":
                    return ExcelReaderFactory.CreateOpenXmlReader(stream);

                case ".xls":
                    return ExcelReaderFactory.CreateBinaryReader(stream);

                case ".csv":
                    return ExcelReaderFactory.CreateCsvReader(stream);

                default:
                    throw new NotSupportedException($"فرمت فایل '{extension}' پشتیبانی نمی‌شود");
            }
        }

        public static bool IsExcelFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension == ".xlsx" || extension == ".xls" || extension == ".csv";
        }
    }

}

