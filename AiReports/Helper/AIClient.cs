
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AiReports.Helper
{

      public class AIClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _apiKey;
        private readonly string _apiEndpoint;
        private readonly string _model;
        private readonly int _maxTokens = 16000;
        public event Action<Exception> OnError;

        public AIClient(string apiKey, string apiEndpoint, string model, ILogger logger, AIClientConfiguration configuration = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _apiEndpoint = apiEndpoint ?? throw new ArgumentNullException(nameof(apiEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient = new HttpClient
            {
                Timeout = configuration.Timeout
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _model = model;
            var config = configuration ?? new AIClientConfiguration();
            _maxTokens = config.MaxTokens;
        }

        public AIResponse CreateRequest(
            string prompt,
            string systemPropmts,
            List<string> imagePaths = null,
            List<string> excelPaths = null,
            List<DataTable> dataTables = null,
            CancellationToken cancellationToken = default)
        {
            var response = new AIResponse(_logger);


            _ = ProcessRequestInBackgroundAsync(
                response,
                prompt,
                systemPropmts,
                imagePaths,
                excelPaths,
                dataTables,
                cancellationToken);

            return response;
        }

        private async Task ProcessRequestInBackgroundAsync(
                AIResponse response,
                string prompt,
                string systemPropmt,
                List<string> imagePaths,
                List<string> excelPaths,
                List<DataTable> dataTables,
                CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("شروع ارسال درخواست به مدل هوش مصنوعی");

                var requestContent = await BuildRequestContentAsync(
                    prompt, systemPropmt, imagePaths, excelPaths, dataTables);

                using (var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint))
                {
                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(requestContent),
                        Encoding.UTF8,
                        "application/json");

                    using (var httpResponse = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken))
                    {
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await httpResponse.Content.ReadAsStringAsync();
                            throw new AIClientException(
                                $"خطا در دریافت پاسخ: {httpResponse.StatusCode}",
                                errorContent);

                        }

                        await ProcessStreamResponseAsync(httpResponse, response, cancellationToken);
                    }
                }

                _logger.LogInformation("دریافت پاسخ با موفقیت به پایان رسید");
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("درخواست توسط کاربر لغو شد");
                OnError?.Invoke(ex);
            }
            catch (Exception ex)
            {

                string errorMessage = ex.Message;

                if (ex.GetType().GetProperty("ErrorDetails") != null)
                {
                    var errorDetails = ex.GetType().GetProperty("ErrorDetails")?.GetValue(ex);
                    if (errorDetails != null)
                    {
                        try
                        {
                            var parsed = JsonConvert.DeserializeObject<ErrorResponse>(errorDetails.ToString());
                            if (parsed?.error?.message != null)
                            {
                                errorMessage = parsed.error.message;
                            }
                        }
                        catch
                        {
                            // اگر پارس نشد، متن JSON خامو نمایش بده
                            errorMessage = errorDetails.ToString();
                        }
                    }
                }

                OnError?.Invoke(ex);
                _logger.LogError($"خطا در ارسال درخواست: {errorMessage}", ex);


            }
        }



        private async Task<object> BuildRequestContentAsync(
            string prompt,
            string systemPropmt,
            List<string> imagePaths,
            List<string> excelPaths,
            List<DataTable> dataTables)
        {



            var content = new ChatRequest
            {
                model = _model,
                stream = true,
                max_tokens = _maxTokens,
                messages = new List<Message>
                {
                    new Message
                    {
                        role = "system",
                        content = systemPropmt
                    },

                    new Message
                    {
                        role = "user",
                        content =  await BuildMessageContentAsync(prompt, imagePaths, excelPaths, dataTables)
                    }
                }
            };



            return content;
        }

        private async Task<byte[]> ReadAllBytesAsync(string path)
        {
            using (var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            {
                var buffer = new byte[fs.Length];
                await fs.ReadAsync(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        private async Task<List<object>> BuildMessageContentAsync(
            string prompt,
            List<string> imagePaths,
            List<string> excelPaths,
            List<DataTable> dataTables)
        {
            var contentParts = new List<object>();

            if (imagePaths != null && imagePaths.Any())
            {
                foreach (var imagePath in imagePaths)
                {
                    try
                    {
                        var imageData = await ReadAllBytesAsync(imagePath);
                        var base64Image = Convert.ToBase64String(imageData);
                        var mimeType = GetImageMimeType(imagePath);

                        contentParts.Add(new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mimeType,
                                data = base64Image
                            }
                        });

                        _logger.LogInformation($"تصویر اضافه شد: {Path.GetFileName(imagePath)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"خطا در خواندن تصویر: {imagePath}", ex);
                    }
                }
            }

            if (excelPaths != null && excelPaths.Any())
            {
                foreach (var excelPath in excelPaths)
                {
                    try
                    {
                        var dataTable = ExcelHelper.ReadExcelFile(excelPath);
                        var jsonData = DataTableToJson(dataTable);

                        contentParts.Add(new
                        {
                            type = "text",
                            text = $"داده Excel از فایل {Path.GetFileName(excelPath)}:\n```json\n{jsonData}\n```"
                        });

                        _logger.LogInformation($"فایل Excel اضافه شد: {Path.GetFileName(excelPath)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"خطا در خواندن Excel: {excelPath}", ex);
                        OnError?.Invoke(ex);
                    }
                }
            }

            if (dataTables != null && dataTables.Any())
            {
                for (int i = 0; i < dataTables.Count; i++)
                {
                    string name = dataTables[i].TableName;
                    try
                    {
                        var jsonData = DataTableToJson(dataTables[i]);

                        contentParts.Add(new
                        {
                            type = "text",
                            text = $"DataTable {i + 1}:\n```json\n{jsonData}\n```"
                        });
                        _logger.LogInformation($" {name} اضافه شد");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"خطا در تبدیل {name}", ex);
                        OnError?.Invoke(ex);
                    }
                }
            }

            contentParts.Add(new
            {
                type = "text",
                text = prompt
            });

            return contentParts;
        }


        private async Task ProcessStreamResponseAsync(
            HttpResponseMessage httpResponse,
            AIResponse response,
            CancellationToken cancellationToken)
        {
            using (var stream = await httpResponse.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                var completeText = new StringBuilder();
                int chunkCount = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // نادیده گرفتن خطوط خالی
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        // حذف پیشوند "data: " اگر وجود داشته باشد
                        var jsonLine = line.Trim();
                        if (jsonLine.StartsWith("data: "))
                        {
                            jsonLine = jsonLine.Substring(6).Trim();
                        }

                        // بررسی پایان stream
                        if (jsonLine == "[DONE]")
                        {
                            _logger.LogInformation("دریافت علامت [DONE] - پایان stream");
                            break;
                        }

                        // نادیده گرفتن خطوط خالی پس از حذف "data:"
                        if (string.IsNullOrWhiteSpace(jsonLine))
                            continue;

                        // Parse کردن JSON
                        var chunk = JsonConvert.DeserializeObject<StreamResponse>(jsonLine);

                        if (chunk?.choices == null || chunk.choices.Count == 0)
                        {
                            _logger.LogInformation("Chunk بدون choices دریافت شد");
                            continue;
                        }

                        var firstChoice = chunk.choices[0];

                        // بررسی پایان stream با finish_reason
                        if (!string.IsNullOrEmpty(firstChoice.finish_reason))
                        {
                            _logger.LogInformation($"Stream به پایان رسید (finish_reason: {firstChoice.finish_reason})");
                            break;
                        }

                        // استخراج محتوا
                        var content = firstChoice.delta?.content;

                        if (!string.IsNullOrEmpty(content))
                        {
                            completeText.Append(content);
                            response.OnChunkReceived(content);
                            chunkCount++;

                            if (chunkCount % 10 == 0) // لاگ هر 10 chunk
                            {
                                _logger.LogInformation($"دریافت {chunkCount} chunk، طول فعلی: {completeText.Length}");
                            }
                        }

                        // لاگ role در اولین chunk
                        if (firstChoice.delta?.role != null)
                        {
                            _logger.LogInformation($"شروع پاسخ با role: {firstChoice.delta.role}");
                        }

                        // لاگ content_filter_results (اختیاری)
                        if (firstChoice.content_filter_results != null)
                        {
                            // _logger.LogInformation("Content filter results دریافت شد");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning($"خطا در parse کردن JSON:\n" +
                            $"Message: {ex.Message}\n" +
                            $"Line: {line.Substring(0, Math.Min(200, line.Length))}...");
                        OnError?.Invoke(ex);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"خطا در پردازش chunk: {ex.Message}", ex);
                        OnError?.Invoke(ex);
                    }
                }

                if (completeText.Length == 0)
                {
                    _logger.LogWarning("⚠️ هیچ محتوایی از stream دریافت نشد!");

                }
                else
                {
                    _logger.LogInformation($"✅ مجموع {chunkCount} chunk دریافت شد ({completeText.Length} کاراکتر)");

                }

                response.CompleteResponse(completeText.ToString());
            }
        }








        private string GetImageMimeType(string imagePath)
        {
            var extension = Path.GetExtension(imagePath).ToLower();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                default:
                    return "image/jpeg";
            }
        }


        private string DataTableToJson(DataTable dataTable)
        {
            if (dataTable == null)
                return "{}";

            var rows = new List<Dictionary<string, object>>();

            foreach (DataRow row in dataTable.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    rowDict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                rows.Add(rowDict);
            }


            var resultObject = new
            {
                TableName = dataTable.TableName,
                Rows = rows
            };

            string json = JsonConvert.SerializeObject(resultObject, Formatting.Indented);

            return json;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }


    public class AIResponse
    {
        private readonly ILogger _logger;
        private string _completeText;

        public event EventHandler<string> ChunkReceived;
        public event EventHandler ResponseCompleted;

        public bool IsCompleted { get; private set; }
        public string CompleteText => _completeText;
        public string ExtractedJson { get; private set; } 

        public AIResponse(ILogger logger)
        {
            _logger = logger;
        }

        internal void OnChunkReceived(string chunk)
        {
            ChunkReceived?.Invoke(this, chunk);
        }

        internal void CompleteResponse(string completeText)
        {
            _completeText = completeText;
            IsCompleted = true;

            _logger.LogInformation($"✅ دریافت پاسخ کامل ({completeText?.Length ?? 0} کاراکتر)");

            if (string.IsNullOrWhiteSpace(completeText))
            {
                _logger.LogError("⚠️ محتوای دریافتی خالی است!");
                ResponseCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            _logger.LogInformation("✅ پاسخ با موفقیت شد");
            ResponseCompleted?.Invoke(this, EventArgs.Empty);
        }

        public string GetReport()
        {
            if (_completeText == null) return null;
            return _completeText;
        }




    }


    public class ErrorResponse
    {
        public ErrorDetail error { get; set; }
    }

    public class ErrorDetail
    {
        public string message { get; set; }
        public string type { get; set; }
        public string param { get; set; }
        public string code { get; set; }
        public string solution { get; set; }
        public string request_id { get; set; }
    }

    public class AIClientException : Exception
    {
        public string ErrorDetails { get; }

        public AIClientException(string message, string errorDetails)
            : base(message)
        {
            ErrorDetails = errorDetails;
        }
    }
    public class StreamResponse
    {
        public string id { get; set; }
        public long created { get; set; }
        public string model { get; set; }

        [JsonProperty("object")]
        public string objectType { get; set; }
        public string system_fingerprint { get; set; }
        public List<Choice> choices { get; set; }
        public object provider_specific_fields { get; set; }
        public object citations { get; set; }
        public string service_tier { get; set; }
        public string obfuscation { get; set; }
    }

    public class Choice
    {
        public string finish_reason { get; set; }
        public int index { get; set; }
        public Delta delta { get; set; }
        public object logprobs { get; set; }
        public ContentFilterResults content_filter_results { get; set; }
    }

    public class Delta
    {
        public object provider_specific_fields { get; set; }
        public object refusal { get; set; }
        public string content { get; set; }
        public string role { get; set; }
        public object function_call { get; set; }
        public object tool_calls { get; set; }
        public object audio { get; set; }
    }

    public class ContentFilterResults
    {
        public FilterResult hate { get; set; }
        public ProtectedMaterialResult protected_material_code { get; set; }
        public ProtectedMaterialResult protected_material_text { get; set; }
        public FilterResult self_harm { get; set; }
        public FilterResult sexual { get; set; }
        public FilterResult violence { get; set; }
    }

    public class FilterResult
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class ProtectedMaterialResult
    {
        public bool filtered { get; set; }
        public bool detected { get; set; }
    }


    public class ContentBlock
    {
        public string type { get; set; }
        public string text { get; set; }
    }


    public class ChatRequest
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public bool stream { get; set; }
        public int max_tokens { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public object content { get; set; }
    }


    public class AIClientConfiguration
    {
        public int MaxTokens { get; set; } = 16000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
        public int MaxRetries { get; set; } = 3;
        public bool EnableDetailedLogging { get; set; } = false;
    }

    public interface ILogger
    {
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
    }


    public class AvalaiApiService
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private static readonly HttpClient _httpClient = new HttpClient();

        public AvalaiApiService(string apiKey, string baseUrl)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        /// <summary>
        /// دریافت فقط مدل‌های Chat
        /// </summary>
        public async Task<List<ModelInfo>> GetChatModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{_baseUrl}/models");
                var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(response);

                // فیلتر کردن فقط مدل‌های chat
                return modelsResponse.Data
                    .Where(m => m.Mode == "chat")
                    .OrderBy(m => m.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در دریافت مدل‌ها: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// دریافت تمام مدل‌ها
        /// </summary>
        public async Task<List<ModelInfo>> GetAllModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{_baseUrl}/models");
                var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(response);
                return modelsResponse.Data;
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در دریافت مدل‌ها: {ex.Message}", ex);
            }
        }
    }

    public class ModelInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("owned_by")]
        public string OwnedBy { get; set; }

        [JsonProperty("min_tier")]
        public int MinTier { get; set; }

        [JsonProperty("pricing")]
        public PricingInfo Pricing { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("max_requests_per_1_minute")]
        public double? MaxRequestsPerMinute { get; set; }

        [JsonProperty("max_tokens_per_1_minute")]
        public double? MaxTokensPerMinute { get; set; }

        // 🔴 تغییر از int? به double? برای حل مشکل
        [JsonProperty("max_tokens")]
        public double? MaxTokens { get; set; }

        [JsonProperty("max_input_tokens")]
        public double? MaxInputTokens { get; set; }

        [JsonProperty("max_output_tokens")]
        public double? MaxOutputTokens { get; set; }

        [JsonProperty("max_images_per_prompt")]
        public double? MaxImagesPerPrompt { get; set; }

        [JsonProperty("max_videos_per_prompt")]
        public double? MaxVideosPerPrompt { get; set; }

        [JsonProperty("max_video_length")]
        public double? MaxVideoLength { get; set; }

        [JsonProperty("max_audio_length_hours")]
        public double? MaxAudioLengthHours { get; set; }

        [JsonProperty("max_audio_per_prompt")]
        public double? MaxAudioPerPrompt { get; set; }

        [JsonProperty("max_pdf_size_mb")]
        public double? MaxPdfSizeMb { get; set; }

        [JsonProperty("supports_system_messages")]
        public bool? SupportsSystemMessages { get; set; }

        [JsonProperty("supports_function_calling")]
        public bool? SupportsFunctionCalling { get; set; }

        [JsonProperty("supports_parallel_function_calling")]
        public bool? SupportsParallelFunctionCalling { get; set; }

        [JsonProperty("supports_vision")]
        public bool? SupportsVision { get; set; }

        [JsonProperty("supports_pdf_input")]
        public bool? SupportsPdfInput { get; set; }

        [JsonProperty("supports_audio_output")]
        public bool? SupportsAudioOutput { get; set; }

        [JsonProperty("supports_prompt_caching")]
        public bool? SupportsPromptCaching { get; set; }

        [JsonProperty("supports_tool_choice")]
        public bool? SupportsToolChoice { get; set; }

        [JsonProperty("supports_response_schema")]
        public bool? SupportsResponseSchema { get; set; }

        [JsonProperty("supports_web_search")]
        public bool? SupportsWebSearch { get; set; }

        [JsonProperty("supports_native_streaming")]
        public bool? SupportsNativeStreaming { get; set; }

        [JsonProperty("supported_endpoints")]
        public List<string> SupportedEndpoints { get; set; }

        public int GetMaxTokensAsInt()
        {
            return MaxTokens.HasValue ? (int)MaxTokens.Value : 0;
        }

        public int GetMaxInputTokensAsInt()
        {
            return MaxInputTokens.HasValue ? (int)MaxInputTokens.Value : 0;
        }

        public int GetMaxOutputTokensAsInt()
        {
            return MaxOutputTokens.HasValue ? (int)MaxOutputTokens.Value : 0;
        }
    }

    public class PricingInfo
    {
        [JsonProperty("input")]
        public double Input { get; set; }

        [JsonProperty("cached_input")]
        public double? CachedInput { get; set; }

        [JsonProperty("output")]
        public double Output { get; set; }

        [JsonProperty("audio_input")]
        public double? AudioInput { get; set; }

        [JsonProperty("audio_cached_input")]
        public double? AudioCachedInput { get; set; }

        [JsonProperty("audio_output")]
        public double? AudioOutput { get; set; }

        [JsonProperty("input_cost_per_page")]
        public double? InputCostPerPage { get; set; }

        [JsonProperty("input_cost_per_character")]
        public double? InputCostPerCharacter { get; set; }

        [JsonProperty("search_context_cost_per_query")]
        public SearchContextCost SearchContextCostPerQuery { get; set; }

        public string GetDisplayPrice()
        {
            if (InputCostPerCharacter.HasValue)
                return $"Per Char: {InputCostPerCharacter.Value:F6}";

            if (InputCostPerPage.HasValue)
                return $"Per Page: {InputCostPerPage.Value:F3} | Out: {Output:F2}";

            var result = $"In: {Input:F2} | Out: {Output:F2}";

            if (CachedInput.HasValue)
                result += $" | Cached: {CachedInput.Value:F2}";

            if (AudioInput.HasValue)
                result += $" | Audio In: {AudioInput.Value:F2}";

            return result;
        }
    }

    public class SearchContextCost
    {
        [JsonProperty("low")]
        public double Low { get; set; }

        [JsonProperty("medium")]
        public double Medium { get; set; }

        [JsonProperty("high")]
        public double High { get; set; }

        public string GetDisplayText()
        {
            return $"Low: {Low:F3} | Med: {Medium:F3} | High: {High:F3}";
        }
    }

    public class ModelsResponse
    {
        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("data")]
        public List<ModelInfo> Data { get; set; }
    }

}