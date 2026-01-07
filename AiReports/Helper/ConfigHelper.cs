using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiReports.Helper
{

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ModelUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int MaxToken { get; set; }
    }

    public static class SettingsService
    {
        private static readonly string FilePath = Path.Combine(Application.StartupPath, "Configs", "AiConfigs.json");
        private static readonly string SystemPromptPath = Path.Combine(Application.StartupPath, "Configs", "SystemPrompt.txt");
        private static readonly string UserPromptPath = Path.Combine(Application.StartupPath, "Configs", "UserPrompt.txt");

        public static void Save(ApiSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(
                    settings,
                    Formatting.Indented
                );

                File.WriteAllText(FilePath, json);
                MessageBox.Show("Save User Prompt Successfuly");
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
           
        }


        public static ApiSettings Load()
        {
            try
            {

                if (!File.Exists(FilePath))
                {
                    var defaultSettings = new ApiSettings
                    {
                        BaseUrl = "",
                         ModelUrl ="",
                        ApiKey = "",
                        MaxToken = 0
                    };

                    Save(defaultSettings);
                    return defaultSettings;
                }

                var json = File.ReadAllText(FilePath);
                return JsonConvert.DeserializeObject<ApiSettings>(json)
                       ?? new ApiSettings();
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return new ApiSettings();
            }
        }

        public static string ReadSystemPrompt()
        {
            try
            {
                if (!File.Exists(SystemPromptPath))
                    return "";

                var sys = File.ReadAllText(SystemPromptPath);
                return sys;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return "";
            }
        }
        public static void SaveSystemPrompt(string systemPrompt)
        {
            try
            {
                File.WriteAllText(SystemPromptPath, systemPrompt);
                MessageBox.Show("Save System Prompt Successfuly");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        public static string ReadUserPrompt()
        {
            try
            {
                if (!File.Exists(UserPromptPath))
                    return "";

                var sys = File.ReadAllText(UserPromptPath);
                return sys;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return "";
            }

        }
        public static void SaveUserPrompt(string UserPrompt)
        {
            try
            {
                File.WriteAllText(UserPromptPath, UserPrompt);
                MessageBox.Show("Save User Prompt Successfuly");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }



    }
}
