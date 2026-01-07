using Markdig;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AiReports.Helper
{
    public static class MarkdownViewerHelper
    {
        public static string ConvertToHtml(string markdownText, string fontPath)
        {

            string rawHtmlBody = PrepareHtmlForDatabase(markdownText);


            return ReconstructHtmlWithFont(rawHtmlBody, fontPath);
        }

        public static string PrepareHtmlForDatabase(string markdownText)
        {

            string cleanMarkdown = CleanAiOutput(markdownText);

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UsePipeTables()
                .UseGridTables()
                .Build();

            return Markdown.ToHtml(cleanMarkdown ?? "", pipeline);
        }


        public static string ReconstructHtmlWithFont(string htmlBodyFromDatabase, string fontPath)
        {
            string fontCss = GetBase64FontCss(fontPath);

            return BuildFinalHtml(htmlBodyFromDatabase, fontCss);
        }

        private static string BuildFinalHtml(string contentBody, string fontCss)
        {
            return $@"<!DOCTYPE html>
                    <html dir='rtl' lang='fa'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <style>
                            /* تزریق فونت */
                            {fontCss}

                            :root {{
                                --primary: #1f2a44;
                                --bg-body: #f3f5f7;
                                --bg-card: #ffffff;
                                --text-main: #333;
                                --border-color: #eee;
                            }}

                            body {{
                                font-family: 'Vazirmatn', Tahoma, sans-serif;
                                background-color: var(--bg-body);
                                color: var(--text-main);
                                padding: 20px;
                                margin: 0;
                            }}

                            .container {{
                                max-width: 1400px;
                                margin: 0 auto;
                                background: var(--bg-card);
                                padding: 40px;
                                border-radius: 12px;
                                box-shadow: 0 5px 20px rgba(0,0,0,0.08);
                            }}

                            /* --- استایل‌های چیدمان --- */
                            .dashboard-row {{
                                display: flex;
                                flex-wrap: wrap;
                                gap: 30px;
                                margin-bottom: 50px;
                                align-items: flex-start;
                                border-bottom: 1px solid #eee;
                                padding-bottom: 30px;
                            }}

                            .dashboard-table-col {{
                                flex: 3;
                                min-width: 400px;
                            }}

                            .dashboard-chart-col {{
                                flex: 2;
                                min-width: 350px;
                                display: flex;
                                flex-direction: column;
                                align-items: center;
                                justify-content: center;
                            }}
        
                            .mermaid {{
                                width: 100%;
                                display: flex;
                                justify-content: center;
                            }}

                            table {{ width: 100%; border-collapse: collapse; font-size: 0.85em; }}
                            th {{ background-color: var(--primary); color: #fff; padding: 10px; }}
                            td {{ padding: 8px; border: 1px solid var(--border-color); }}
                            tr:nth-child(even) {{ background-color: #f9f9f9; }}
        
                            h1, h2, h3 {{ color: var(--primary); }}
                            h1 {{ border-bottom: 3px solid #1e88e5; padding-bottom: 10px; }}
        
                        </style>
                        <script src='https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js'></script>
                    </head>
                    <body>
                        <div class='container'>
                            {contentBody}
                        </div>

                        <script>
                            mermaid.initialize({{
                                startOnLoad: false,
                                theme: 'base',
                                themeVariables: {{
                                    fontFamily: 'Vazirmatn',
                                    fontSize: '13px',
                                    pieSectionTextSize: '12px',
                                    darkMode: false
                                }}
                            }});

                            document.addEventListener('DOMContentLoaded', function() {{
                                const codeBlocks = document.querySelectorAll('pre code.language-mermaid, pre code.mermaid');
                                codeBlocks.forEach(el => {{
                                    const newDiv = document.createElement('div');
                                    newDiv.className = 'mermaid';
                                    newDiv.textContent = el.innerText;
                                    el.parentElement.replaceWith(newDiv);
                                }});
                                mermaid.run();
                            }});
                        </script>
                    </body>
                    </html>";
        }

        private static string GetBase64FontCss(string fontPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(fontPath) && File.Exists(fontPath))
                {
                    byte[] fontBytes = File.ReadAllBytes(fontPath);
                    string base64String = Convert.ToBase64String(fontBytes);
                    return $@"
                            @font-face {{
                                font-family: 'Vazirmatn';
                                src: url(data:font/woff2;charset=utf-8;base64,{base64String}) format('woff2');
                            }}";
                }
            }
            catch { }

            return "";
        }

        private static string CleanAiOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();

            var startPattern = new Regex(@"^```[a-zA-Z]*\s+");
            var endPattern = new Regex(@"\s+```$");

            if (text.StartsWith("```"))
                text = startPattern.Replace(text, "");
            if (text.EndsWith("```"))

                text = endPattern.Replace(text, "");

            return text.Trim();
        }
    }
}

