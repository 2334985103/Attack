using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;

namespace Attack
{
    public class PlaybackRecord
    {
        public string FilePath { get; set; }
        public double Position { get; set; } // 播放位置（秒）
        public DateTime LastPlayed { get; set; }
    }

    public class PlaybackState
    {
        public string CurrentFolderPath { get; set; }
        public string CurrentVideoPath { get; set; }
        public double PlaybackPosition { get; set; }
        public DateTime LastPlayed { get; set; }
    }

    public class PlaybackRecordManager
    {
        private static readonly string RecordFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AttackPlayer",
            "playback_records.json");

        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AttackPlayer",
            "playback_state.json");

        private Dictionary<string, PlaybackRecord> _records;
        private PlaybackState _currentState;

        public PlaybackRecordManager()
        {
            _records = new Dictionary<string, PlaybackRecord>();
            _currentState = new PlaybackState();
            LoadRecords();
            LoadPlaybackState();
        }

        // 加载播放记录
        private void LoadRecords()
        {
            try
            {
                if (File.Exists(RecordFilePath))
                {
                    string json = File.ReadAllText(RecordFilePath);
                    var records = JsonConvert.DeserializeObject<List<PlaybackRecord>>(json);

                    if (records != null)
                    {
                        _records = records.ToDictionary(r => r.FilePath, r => r);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载播放记录失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 加载播放状态
        private void LoadPlaybackState()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    _currentState = JsonConvert.DeserializeObject<PlaybackState>(json) ?? new PlaybackState();
                }
            }
            catch (Exception ex)
            {
                // 静默失败，不显示错误信息
                System.Diagnostics.Debug.WriteLine($"加载播放状态失败: {ex.Message}");
            }
        }

        // 保存播放记录
        private void SaveRecords()
        {
            try
            {
                string directory = Path.GetDirectoryName(RecordFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(_records.Values.ToList(), Formatting.Indented);
                File.WriteAllText(RecordFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存播放记录失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 保存播放状态
        private void SavePlaybackState()
        {
            try
            {
                string directory = Path.GetDirectoryName(StateFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(_currentState, Formatting.Indented);
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存播放状态失败: {ex.Message}");
            }
        }

        // 获取播放记录
        public PlaybackRecord GetRecord(string filePath)
        {
            if (_records.ContainsKey(filePath))
            {
                return _records[filePath];
            }
            return null;
        }

        // 更新播放记录
        public void UpdateRecord(string filePath, double position)
        {
            var record = new PlaybackRecord
            {
                FilePath = filePath,
                Position = position,
                LastPlayed = DateTime.Now
            };

            _records[filePath] = record;
            SaveRecords();
        }

        // 删除播放记录
        public void RemoveRecord(string filePath)
        {
            if (_records.ContainsKey(filePath))
            {
                _records.Remove(filePath);
                SaveRecords();
            }
        }

        // 清除所有记录
        public void ClearAllRecords()
        {
            _records.Clear();
            SaveRecords();

            // 同时清除播放状态
            _currentState = new PlaybackState();
            if (File.Exists(StateFilePath))
            {
                File.Delete(StateFilePath);
            }
        }

        // 获取最近播放的文件
        public List<PlaybackRecord> GetRecentRecords(int count = 10)
        {
            return _records.Values
                .OrderByDescending(r => r.LastPlayed)
                .Take(count)
                .ToList();
        }

        // 获取当前播放状态
        public PlaybackState GetCurrentPlaybackState()
        {
            return _currentState;
        }

        // 更新播放状态
        public void UpdatePlaybackState(string folderPath, string videoPath, double position)
        {
            _currentState.CurrentFolderPath = folderPath;
            _currentState.CurrentVideoPath = videoPath;
            _currentState.PlaybackPosition = position;
            _currentState.LastPlayed = DateTime.Now;

            SavePlaybackState();
        }
    }
}