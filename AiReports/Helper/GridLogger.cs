using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiReports.Helper
{
    public enum LogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Success = 3
    }

    public class LogEntry : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private LogLevel _level;
        private string _message;
        private string _details;

        public DateTime Timestamp
        {
            get { return _timestamp; }
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        public LogLevel Level
        {
            get { return _level; }
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public string Details
        {
            get { return _details; }
            set
            {
                _details = value;
                OnPropertyChanged(nameof(Details));
            }
        }

        public string LevelText
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Info:
                        return "اطلاعات";
                    case LogLevel.Warning:
                        return "هشدار";
                    case LogLevel.Error:
                        return "خطا";
                    case LogLevel.Success:
                        return "موفق";
                    default:
                        return "نامشخص";
                }
            }
        }

        public Color LevelColor
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Info:
                        return Color.FromArgb(33, 150, 243);     
                    case LogLevel.Warning:
                        return Color.FromArgb(255, 152, 0);      
                    case LogLevel.Error:
                        return Color.FromArgb(244, 67, 54);       
                    case LogLevel.Success:
                        return Color.FromArgb(76, 175, 80);      
                    default:
                        return Color.Gray;
                }
            }
        }

        public string IconName
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Info:
                        return "info";
                    case LogLevel.Warning:
                        return "warning";
                    case LogLevel.Error:
                        return "error";
                    case LogLevel.Success:
                        return "apply";
                    default:
                        return "question";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



    public class GridLogger : ILogger
    {
        private readonly BindingList<LogEntry> _logs;
        private readonly DataGridView _gridControl;
        private readonly int _maxLogEntries;
        private readonly SynchronizationContext _syncContext;

        public BindingList<LogEntry> Logs
        {
            get { return _logs; }
        }

        public GridLogger(DataGridView gridControl, int maxLogEntries = 1000)
        {
            if (gridControl == null)
                throw new ArgumentNullException(nameof(gridControl));

            _gridControl = gridControl;
            _maxLogEntries = maxLogEntries;
            _logs = new BindingList<LogEntry>();
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

            ConfigureGridAppearance();

            _gridControl.DataSource = _logs;

            _gridControl.CellFormatting += _gridControl_CellFormatting;

            _gridControl.CellDoubleClick += _gridControl_CellDoubleClick;
        }

        private void ConfigureGridAppearance()
        {
            _gridControl.AutoGenerateColumns = false; 
            _gridControl.AllowUserToAddRows = false;
            _gridControl.AllowUserToDeleteRows = false;
            _gridControl.ReadOnly = true;
            _gridControl.RowHeadersVisible = false;
            _gridControl.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridControl.MultiSelect = false;
            _gridControl.BackgroundColor = Color.White;
            _gridControl.BorderStyle = BorderStyle.None;
            _gridControl.GridColor = Color.WhiteSmoke;
            _gridControl.RowTemplate.Height = 30; 


            var colLevel = new DataGridViewTextBoxColumn();
            colLevel.DataPropertyName = "LevelText"; 
            colLevel.HeaderText = "وضعیت";
            colLevel.Width = 80;
            colLevel.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridControl.Columns.Add(colLevel);

            var colTime = new DataGridViewTextBoxColumn();
            colTime.DataPropertyName = "Timestamp";
            colTime.HeaderText = "زمان";
            colTime.Width = 80;
            colTime.DefaultCellStyle.Format = "HH:mm:ss.ff"; 
            _gridControl.Columns.Add(colTime);

            // ستون پیام
            var colMsg = new DataGridViewTextBoxColumn();
            colMsg.DataPropertyName = "Message";
            colMsg.HeaderText = "پیام سیستم";
            colMsg.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; 
            _gridControl.Columns.Add(colMsg);
        }

        // این متد جادوی زیباسازی را انجام می‌دهد
        private void _gridControl_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _logs.Count)
            {
                var logEntry = _logs[e.RowIndex];

                e.CellStyle.ForeColor = logEntry.LevelColor;
                e.CellStyle.SelectionForeColor = logEntry.LevelColor;
                e.CellStyle.SelectionBackColor = Color.FromArgb(240, 240, 240); // رنگ سلکت ملایم

                if (logEntry.Level == LogLevel.Error)
                {
                    e.CellStyle.Font = new Font(_gridControl.Font, FontStyle.Bold);
                }
            }
        }

        private void _gridControl_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var entry = _logs[e.RowIndex];
                if (!string.IsNullOrEmpty(entry.Details))
                {
                    MessageBox.Show(entry.Details, "جزئیات فنی", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(entry.Message, "پیام", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public void LogInformation(string message) => AddLog(LogLevel.Info, message);
        public void LogWarning(string message) => AddLog(LogLevel.Warning, message);
        public void LogSuccess(string message) => AddLog(LogLevel.Success, message);

        public void LogError(string message, Exception ex = null)
        {
            string details = null;
            if (ex != null)
            {
                details = $"{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            }
            AddLog(LogLevel.Error, message, details);
        }

        private void AddLog(LogLevel level, string message, string details = null)
        {
            _syncContext.Post(delegate
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Details = details
                };

                _logs.Insert(0, entry);

                while (_logs.Count > _maxLogEntries)
                {
                    _logs.RemoveAt(_logs.Count - 1);
                }


                if (_gridControl.Rows.Count > 0)
                {
                    _gridControl.ClearSelection();

                }

            }, null);
        }

        public void ClearLogs()
        {
            _syncContext.Post(d => _logs.Clear(), null);
        }

        public List<LogEntry> GetLogsByLevel(LogLevel level) => _logs.Where(l => l.Level == level).ToList();
        public int GetLogCount() => _logs.Count;
        public int GetErrorCount() => _logs.Count(l => l.Level == LogLevel.Error);
        public int GetWarningCount() => _logs.Count(l => l.Level == LogLevel.Warning);
    }

}
