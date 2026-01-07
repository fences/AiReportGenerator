using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using SectionProperties = DocumentFormat.OpenXml.Wordprocessing.SectionProperties;

namespace AiReports.Helper
{
    public class HtmlReportToWord
    {
        public static void ConvertHtmlToDocx(string rawHtml, string outputDocxPath)
        {


            string processedHtml = PreProcessHtml(rawHtml);

            string styledHtml = AddProfessionalStyles(processedHtml);

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(outputDocxPath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                SetPageLayout(body);

                string altChunkId = "AltChunkId1";
                AlternativeFormatImportPart chunk = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.Html, altChunkId);

                using (Stream chunkStream = chunk.GetStream(FileMode.Create, FileAccess.Write))
                using (StreamWriter stringWriter = new StreamWriter(chunkStream, Encoding.UTF8))
                {
                    stringWriter.Write(styledHtml);
                }

                AltChunk altChunk = new AltChunk();
                altChunk.Id = altChunkId;
                body.Append(altChunk);

                mainPart.Document.Save();
            }
        }

        private static string PreProcessHtml(string html)
        {

            html = ApplyCellColor(html, "🔴", "#FFC7CE");


            html = ApplyCellColor(html, "🟠", "#FFEB9C");

            html = ApplyCellColor(html, "🟢", "#C6EFCE");


            html = Regex.Replace(html, @"\p{Cs}|\p{So}|\p{Cn}", match =>
            {

                return "";
            });

            return html;
        }

        private static string ApplyCellColor(string html, string emoji, string hexColor)
        {

            string pattern = $@"<td([^>]*)>(.*?)({emoji})(.*?)<\/td>";

            return Regex.Replace(html, pattern, match =>
            {
                string existingAttributes = match.Groups[1].Value;
                string contentBefore = match.Groups[2].Value;
                string contentAfter = match.Groups[4].Value;


                string newStyle = $"background-color: {hexColor};";
                string newAttributes;

                if (existingAttributes.Contains("style='") || existingAttributes.Contains("style=\""))
                {

                    newAttributes = existingAttributes + $" style=\"{newStyle}\"";
                }
                else
                {
                    newAttributes = existingAttributes + $" style=\"{newStyle}\"";
                }

                return $"<td{newAttributes}>{contentBefore}{contentAfter}</td>";
            }, RegexOptions.Singleline);
        }


        private static string AddProfessionalStyles(string htmlContent)
        {
            return $@"
        <!DOCTYPE html>
        <html dir='rtl'>
        <head>
            <meta charset='UTF-8'>
            <style>
                body {{
                    font-family: 'B Zar', 'Arial', sans-serif;
                    font-size: 14pt;
                    text-align: justify;
                    direction: rtl;
                    line-height: 1.5;
                }}
                
                /* --- استایل‌های دقیق جدول --- */
                table {{
                    width: 100%;
                    border-collapse: collapse;
                    margin-bottom: 20px;
                    font-family: 'B Zar';
                    font-size: 12pt; /* فونت جدول کمی ریزتر برای جا شدن بهتر */
                }}
                
                th, td {{
                    border: 1px solid #000;
                    padding: 3px; /* پدینگ کم برای کاهش ارتفاع */
                    vertical-align: middle; /* تراز عمودی: وسط */
                    text-align: center; /* تراز افقی: وسط */
                    height: auto;
                }}

                th {{
                    background-color: #D9D9D9;
                    font-weight: bold;
                    font-size: 12pt;
                }}

                /* --- نکته کلیدی برای حذف فاصله‌های اضافی (Remove Space) --- */
                /* این بخش باعث می‌شود پاراگراف‌های داخل جدول هیچ فاصله‌ای نداشته باشند */
                table p {{
                    margin-top: 0 !important;
                    margin-bottom: 0 !important;
                    padding: 0 !important;
                    line-height: 1.2; /* فاصله خطوط فشرده‌تر داخل جدول */
                    text-align: center; /* اطمینان از وسط‌چین بودن متن پاراگراف */
                }}

                /* اگر جایی نیاز به چپ‌چین بود (مثل کدها یا اعداد خاص) */
                .ltr-text {{
                    direction: ltr;
                    text-align: left;
                }}

                ul, ol {{ margin-right: 25px; }}
            </style>
        </head>
        <body>
            {htmlContent}
        </body>
        </html>";
        }
        private static void SetPageLayout(Body body)
        {
            SectionProperties sectionProps = new SectionProperties();
            PageSize pageSize = new PageSize() { Width = 11906U, Height = 16838U, Orient = PageOrientationValues.Portrait };
            PageMargin pageMargin = new PageMargin()
            {
                Top = 1440,
                Right = 1440,
                Bottom = 1440,
                Left = 1440,
                Header = 720,
                Footer = 720,
                Gutter = 0
            };
            BiDi bidi = new BiDi();
            sectionProps.Append(pageSize, pageMargin, bidi);
            body.Append(sectionProps);
        }
    }
}
