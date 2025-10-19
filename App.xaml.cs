using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
// 使用别名避免命名冲突
using WinForms = System.Windows.Forms;

namespace Attack
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer progressTimer;
        private DispatcherTimer memorySaveTimer;
        private DispatcherTimer controlsHideTimer;
        private bool isDraggingProgress = false;
        private bool isFullscreen = false;
        private bool isVideoFullscreen = false;
        private WindowState previousWindowState;
        private WindowStyle previousWindowStyle;
        private WindowState videoFullscreenPreviousWindowState;
        private WindowStyle videoFullscreenPreviousWindowStyle;
        private ResizeMode videoFullscreenPreviousResizeMode;
        private GridLength videoFullscreenPreviousColumnWidth;
        private string currentFolderPath;
        private List<VideoFile> videoFiles;
        private bool isPlaying = false;
        private Border progressBackground;
        private Thumb progressThumb;
        private Border trackBackground;
        private PlaybackRecordManager recordManager;
        private string currentVideoPath;
        private Button FullscreenSpeedButton;

        // 倍速选项
        private readonly string[] speedOptions = new[] { "0.5x", "0.75x", "1.0x", "1.25x", "1.5x", "2.0x" };
        private readonly double[] speedValues = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };
        private int currentSpeedIndex = 2; // 默认1.0x

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
            HandleCommandLineArgs();
        }

        private void InitializePlayer()
        {


           FullscreenSpeedButton = new Button();
           FullscreenSpeedButton.Content = speedOptions[currentSpeedIndex];


            // 初始化进度计时器
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(50);
            progressTimer.Tick += ProgressTimer_Tick;

            // 初始化记忆播放保存计时器（每10秒保存一次）
            memorySaveTimer = new DispatcherTimer();
            memorySaveTimer.Interval = TimeSpan.FromSeconds(10);
            memorySaveTimer.Tick += MemorySaveTimer_Tick;

            // 初始化控制条隐藏计时器
            controlsHideTimer = new DispatcherTimer();
            controlsHideTimer.Interval = TimeSpan.FromSeconds(3);
            controlsHideTimer.Tick += ControlsHideTimer_Tick;

            // 设置初始音量
            MediaPlayer.Volume = VolumeSlider.Value;

            // 设置速度选项
            SpeedComboBox.SelectedIndex = currentSpeedIndex;

            // 初始化视频文件列表
            videoFiles = new List<VideoFile>();

            // 初始化播放记录管理器
            recordManager = new PlaybackRecordManager();

            // 恢复上次播放状态
            RestoreLastPlaybackState();
        }



        private void VideoContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (MediaPlayer.Source != null)
            {
                if (isVideoFullscreen)
                {
                    // 显示控制条和按钮
                    if (FullscreenControls.Opacity < 1)
                    {
                        var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.3)
                        };
                        FullscreenControls.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    }

                    // 重置隐藏计时器
                    controlsHideTimer.Stop();
                    controlsHideTimer.Start();
                }
            }
        }

        private void ControlsHideTimer_Tick(object sender, EventArgs e)
        {
            if (isVideoFullscreen && FullscreenControls.Opacity > 0)
            {
                // 隐藏控制条和按钮
                var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                FullscreenControls.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
            }
            controlsHideTimer.Stop();
        }

        private void FullscreenControls_MouseMove(object sender, MouseEventArgs e)
        {
            // 在控制条上移动时也重置计时器
            controlsHideTimer.Stop();
            controlsHideTimer.Start();
            e.Handled = true;
        }

        private void VideoContainer_MouseLeave(object sender, MouseEventArgs e)
        {
            // 非全屏时不隐藏控制条，所以只处理全屏状态
            if (isVideoFullscreen && FullscreenControls.Opacity > 0)
            {
                var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                FullscreenControls.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
            }
        }

        private void MediaPlayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击视频切换播放/暂停
            if (isVideoFullscreen)
            {
                FullscreenPlayPauseButton_Click(sender, e);
            }
            else
            {
                NormalPlayPauseButton_Click(sender, e);
            }
        }

        private void NormalProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        {
            // 鼠标进入进度条时显示滑块
            var thumb = NormalProgressSlider.Template.FindName("Thumb", NormalProgressSlider) as Thumb;
            if (thumb != null)
            {
                thumb.Opacity = 1;
            }
        }

        private void NormalProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        {
            // 鼠标离开进度条时隐藏滑块
            var thumb = NormalProgressSlider.Template.FindName("Thumb", NormalProgressSlider) as Thumb;
            if (thumb != null && !thumb.IsMouseOver)
            {
                thumb.Opacity = 0;
            }
        }

        private void FullscreenProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        {
            // 鼠标进入进度条时显示滑块
            var thumb = FullscreenProgressSlider.Template.FindName("Thumb", FullscreenProgressSlider) as Thumb;
            if (thumb != null)
            {
                thumb.Opacity = 1;
            }
        }

        private void FullscreenProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        {
            // 鼠标离开进度条时隐藏滑块
            var thumb = FullscreenProgressSlider.Template.FindName("Thumb", FullscreenProgressSlider) as Thumb;
            if (thumb != null && !thumb.IsMouseOver)
            {
                thumb.Opacity = 0;
            }
        }

        private void NormalFullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleVideoFullscreen();
        }

        private void FullscreenFullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleVideoFullscreen();
        }

        private void ToggleVideoFullscreen()
        {
            if (!isVideoFullscreen)
            {
                // 进入视频全屏
                videoFullscreenPreviousWindowState = WindowState;
                videoFullscreenPreviousWindowStyle = WindowStyle;
                videoFullscreenPreviousResizeMode = ResizeMode;
                videoFullscreenPreviousColumnWidth = MainGrid.ColumnDefinitions[0].Width;

                // 隐藏侧边栏
                MainGrid.ColumnDefinitions[0].Width = new GridLength(0);

                // 设置窗口全屏
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;

                // 隐藏底部控制面板
                ControlPanel.Visibility = Visibility.Collapsed;

                // 切换控制条
                NormalControls.Visibility = Visibility.Collapsed;
                FullscreenControls.Visibility = Visibility.Visible;

                isVideoFullscreen = true;

                // 显示控制条
                var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                FullscreenControls.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);

                // 启动隐藏计时器
                controlsHideTimer.Start();
            }
            else
            {
                // 退出视频全屏
                WindowStyle = videoFullscreenPreviousWindowStyle;
                WindowState = videoFullscreenPreviousWindowState;
                ResizeMode = videoFullscreenPreviousResizeMode;

                // 恢复侧边栏
                MainGrid.ColumnDefinitions[0].Width = videoFullscreenPreviousColumnWidth;

                // 显示底部控制面板
                ControlPanel.Visibility = Visibility.Visible;

                // 切换控制条
                NormalControls.Visibility = Visibility.Visible;
                FullscreenControls.Visibility = Visibility.Collapsed;

                isVideoFullscreen = false;

                // 停止隐藏计时器
                controlsHideTimer.Stop();
            }
        }

        private void NormalPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void FullscreenPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void TogglePlayPause()
        {
            if (MediaPlayer.Source == null) return;

            try
            {
                if (isPlaying)
                {
                    MediaPlayer.Pause();
                    NormalPlayPauseIcon.Text = "▶";
                    FullscreenPlayPauseIcon.Text = "▶";
                    PlayPauseButton.Content = "▶️ 播放";
                    isPlaying = false;
                    memorySaveTimer.Stop();

                    // 暂停时保存播放进度
                    if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
                    {
                        recordManager.UpdateRecord(currentVideoPath, MediaPlayer.Position.TotalSeconds);

                        // 同时更新播放状态
                        if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                        {
                            recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, MediaPlayer.Position.TotalSeconds);
                        }
                    }
                }
                else
                {
                    MediaPlayer.Play();
                    NormalPlayPauseIcon.Text = "⏸";
                    FullscreenPlayPauseIcon.Text = "⏸";
                    PlayPauseButton.Content = "⏸️ 暂停";
                    isPlaying = true;
                    memorySaveTimer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放控制错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NormalSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            CyclePlaybackSpeed();
        }

        private void FullscreenSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            CyclePlaybackSpeed();
        }

        private void CyclePlaybackSpeed()
        {
            // 循环切换倍速
            currentSpeedIndex = (currentSpeedIndex + 1) % speedOptions.Length;
            double speed = speedValues[currentSpeedIndex];
            string speedText = speedOptions[currentSpeedIndex];

            // 设置播放速度
            MediaPlayer.SpeedRatio = speed;

            // 更新按钮文本
            NormalSpeedButton.Content = speedText;
            FullscreenSpeedButton.Content = speedText;

            // 更新主控制面板的倍速选择
            SpeedComboBox.SelectedIndex = currentSpeedIndex;
        }

        private void NormalProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!MediaPlayer.NaturalDuration.HasTimeSpan) return;

            if (isDraggingProgress)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(NormalProgressSlider.Value);
                UpdateNormalTimeDisplay();
                UpdateProgressDisplay();
            }
        }

        private void FullscreenProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!MediaPlayer.NaturalDuration.HasTimeSpan) return;

            if (isDraggingProgress)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(FullscreenProgressSlider.Value);
                UpdateFullscreenTimeDisplay();
                UpdateProgressDisplay();
            }
        }

        private void NormalProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = true;
            if (isPlaying)
            {
                MediaPlayer.Pause();
            }
        }

        private void NormalProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = false;
            if (MediaPlayer.Source != null && isPlaying)
            {
                MediaPlayer.Play();
            }

            // 拖动结束后保存播放进度
            if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                recordManager.UpdateRecord(currentVideoPath, MediaPlayer.Position.TotalSeconds);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, MediaPlayer.Position.TotalSeconds);
                }
            }
        }

        private void FullscreenProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = true;
            if (isPlaying)
            {
                MediaPlayer.Pause();
            }
        }

        private void FullscreenProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = false;
            if (MediaPlayer.Source != null && isPlaying)
            {
                MediaPlayer.Play();
            }

            // 拖动结束后保存播放进度
            if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                recordManager.UpdateRecord(currentVideoPath, MediaPlayer.Position.TotalSeconds);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, MediaPlayer.Position.TotalSeconds);
                }
            }
        }

        private void UpdateNormalTimeDisplay()
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                NormalCurrentTimeText.Text = MediaPlayer.Position.ToString(@"hh\:mm\:ss");
            }
        }

        private void UpdateFullscreenTimeDisplay()
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                FullscreenCurrentTimeText.Text = MediaPlayer.Position.ToString(@"hh\:mm\:ss");
            }
        }

        private void PrevEpisodeButton_Click(object sender, RoutedEventArgs e)
        {
            PlayPreviousVideo();
        }

        private void NextEpisodeButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNextVideo();
        }

        private void PlayPreviousVideo()
        {
            if (videoFiles.Count > 0 && FileListBox.SelectedIndex > 0)
            {
                FileListBox.SelectedIndex--;
                var prevVideo = videoFiles[FileListBox.SelectedIndex];
                LoadVideoFile(prevVideo.FullPath);
            }
        }

        // 以下为原有代码，保持不变
        private void RestoreLastPlaybackState()
        {
            try
            {
                if (MemoryPlaybackCheckBox.IsChecked == true)
                {
                    var playbackState = recordManager.GetCurrentPlaybackState();

                    // 如果存在上次播放的文件夹，加载文件夹
                    if (!string.IsNullOrEmpty(playbackState.CurrentFolderPath) &&
                        Directory.Exists(playbackState.CurrentFolderPath))
                    {
                        LoadVideoFilesFromFolder(playbackState.CurrentFolderPath);

                        // 如果存在上次播放的视频文件，加载该文件
                        if (!string.IsNullOrEmpty(playbackState.CurrentVideoPath) &&
                            File.Exists(playbackState.CurrentVideoPath))
                        {
                            // 延迟加载视频文件，确保UI已经更新
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                LoadVideoFileWithMemory(playbackState.CurrentVideoPath, playbackState.PlaybackPosition);
                                SelectCurrentFileInList(playbackState.CurrentVideoPath);
                            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                            return;
                        }
                    }
                }

                // 如果没有记忆播放或恢复失败，显示欢迎界面
                ShowWelcomeScreen();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复播放状态失败: {ex.Message}");
                ShowWelcomeScreen();
            }
        }

        private void MemorySaveTimer_Tick(object sender, EventArgs e)
        {
            // 定期保存播放进度
            if (isPlaying && MediaPlayer.NaturalDuration.HasTimeSpan && !string.IsNullOrEmpty(currentVideoPath))
            {
                double currentPosition = MediaPlayer.Position.TotalSeconds;
                recordManager.UpdateRecord(currentVideoPath, currentPosition);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, currentPosition);
                }
            }
        }

        // 在窗口加载完成后获取进度条元素的引用
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            // 获取进度条背景元素的引用
            progressBackground = ProgressSlider.Template.FindName("ProgressBackground", ProgressSlider) as Border;
            progressThumb = ProgressSlider.Template.FindName("Thumb", ProgressSlider) as Thumb;
            trackBackground = ProgressSlider.Template.FindName("TrackBackground", ProgressSlider) as Border;

            if (progressThumb != null)
            {
                progressThumb.DragStarted += ProgressThumb_DragStarted;
                progressThumb.DragDelta += ProgressThumb_DragDelta;
                progressThumb.DragCompleted += ProgressThumb_DragCompleted;
            }

            // 初始更新进度条显示
            UpdateProgressDisplay();
        }

        private void ProgressThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            isDraggingProgress = true;
            if (isPlaying)
            {
                MediaPlayer.Pause();
            }
        }

        private void ProgressThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                // 计算新的进度位置
                double newValue = ProgressSlider.Value + (e.HorizontalChange / ProgressSlider.ActualWidth) * ProgressSlider.Maximum;
                newValue = Math.Max(0, Math.Min(ProgressSlider.Maximum, newValue));

                ProgressSlider.Value = newValue;

                // 立即更新视频位置
                MediaPlayer.Position = TimeSpan.FromSeconds(newValue);
                UpdateTimeDisplay();
                UpdateProgressDisplay();
            }
        }

        private void ProgressThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isDraggingProgress = false;
            if (MediaPlayer.Source != null && isPlaying)
            {
                MediaPlayer.Play();
            }

            // 拖动结束后保存播放进度
            if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                recordManager.UpdateRecord(currentVideoPath, MediaPlayer.Position.TotalSeconds);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, MediaPlayer.Position.TotalSeconds);
                }
            }
        }

        // 点击进度条轨道跳转
        private void TrackBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                Point clickPoint = e.GetPosition(trackBackground);
                double newValue = (clickPoint.X / trackBackground.ActualWidth) * ProgressSlider.Maximum;
                newValue = Math.Max(0, Math.Min(ProgressSlider.Maximum, newValue));

                ProgressSlider.Value = newValue;
                MediaPlayer.Position = TimeSpan.FromSeconds(newValue);
                UpdateTimeDisplay();
                UpdateProgressDisplay();

                // 点击跳转后保存播放进度
                if (!string.IsNullOrEmpty(currentVideoPath))
                {
                    recordManager.UpdateRecord(currentVideoPath, newValue);

                    // 同时更新播放状态
                    if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                    {
                        recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, newValue);
                    }
                }
            }
        }

        // 更新进度条显示
        private void UpdateProgressDisplay()
        {
            if (progressBackground != null && progressThumb != null && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                double progressPercentage = MediaPlayer.Position.TotalSeconds / MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                progressPercentage = Math.Max(0, Math.Min(1, progressPercentage)); // 确保在0-1范围内

                // 设置进度条宽度
                progressBackground.Width = ProgressSlider.ActualWidth * progressPercentage;

                // 更新滑块位置
                UpdateThumbPosition(progressPercentage);
            }
            else if (progressBackground != null && progressThumb != null)
            {
                // 如果没有视频，进度条宽度为0，滑块在起点
                progressBackground.Width = 0;
                UpdateThumbPosition(0);
            }
        }

        // 更新滑块位置
        private void UpdateThumbPosition(double progressPercentage)
        {
            if (progressThumb != null)
            {
                // 计算滑块的位置（考虑滑块自身的宽度）
                double thumbPosition = (ProgressSlider.ActualWidth - progressThumb.Width) * progressPercentage;

                // 设置滑块的左边距
                progressThumb.Margin = new Thickness(thumbPosition, 0, 0, 0);
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (!isDraggingProgress && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = MediaPlayer.Position.TotalSeconds;
                NormalProgressSlider.Value = MediaPlayer.Position.TotalSeconds;
                FullscreenProgressSlider.Value = MediaPlayer.Position.TotalSeconds;
                UpdateTimeDisplay();
                UpdateNormalTimeDisplay();
                UpdateFullscreenTimeDisplay();
                UpdateProgressDisplay(); // 更新进度条视觉显示
            }
        }

        private void ShowWelcomeScreen()
        {
            WelcomeOverlay.Visibility = Visibility.Visible;
        }

        private void HideWelcomeScreen()
        {
            WelcomeOverlay.Visibility = Visibility.Collapsed;
        }

        private void HandleCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string filePath = args[1];
                if (System.IO.File.Exists(filePath) && IsVideoFile(filePath))
                {
                    LoadVideoFileWithMemory(filePath);

                    // 如果文件在文件夹中，尝试加载该文件夹的其他视频文件
                    string folderPath = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        LoadVideoFilesFromFolder(folderPath);
                        SelectCurrentFileInList(filePath);
                    }
                }
            }
        }

        private bool IsVideoFile(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLower();
            return extension == ".mp4" || extension == ".m4v" || extension == ".avi" ||
                   extension == ".mkv" || extension == ".wmv" || extension == ".mov";
        }

        private void LoadVideoFileWithMemory(string filePath, double startPosition = 0)
        {
            try
            {
                ShowStatus("加载视频中...");
                HideWelcomeScreen();

                // 停止当前播放
                MediaPlayer.Stop();
                isPlaying = false;
                memorySaveTimer.Stop();

                // 保存当前文件的播放进度
                if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    recordManager.UpdateRecord(currentVideoPath, MediaPlayer.Position.TotalSeconds);
                }

                currentVideoPath = filePath;

                // 检查是否有记忆播放记录
                if (MemoryPlaybackCheckBox.IsChecked == true)
                {
                    var record = recordManager.GetRecord(filePath);
                    if (record != null && record.Position > 0)
                    {
                        startPosition = record.Position;
                    }
                }

                // 加载新文件
                MediaPlayer.Source = new Uri(filePath);

                // 如果有记忆位置，设置播放位置
                if (startPosition > 0)
                {
                    // 延迟设置播放位置，确保媒体已加载
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            MediaPlayer.Position = TimeSpan.FromSeconds(startPosition);
                            ProgressSlider.Value = startPosition;
                            NormalProgressSlider.Value = startPosition;
                            FullscreenProgressSlider.Value = startPosition;
                            UpdateTimeDisplay();
                            UpdateNormalTimeDisplay();
                            UpdateFullscreenTimeDisplay();
                            UpdateProgressDisplay();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"设置播放位置失败: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }

                MediaPlayer.Play();
                isPlaying = true;
                memorySaveTimer.Start();

                CurrentFileText.Text = Path.GetFileName(filePath);
                PlayPauseButton.Content = "⏸️ 暂停";
                NormalPlayPauseIcon.Text = "⏸";
                FullscreenPlayPauseIcon.Text = "⏸";

                // 更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, filePath, startPosition);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"加载文件错误: {ex.Message}");
                MessageBox.Show($"无法加载视频文件:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 修改现有的 LoadVideoFile 方法，调用新的记忆播放版本
        private void LoadVideoFile(string filePath)
        {
            LoadVideoFileWithMemory(filePath);
        }

        private void LoadVideoFilesFromFolder(string folderPath)
        {
            try
            {
                currentFolderPath = folderPath;
                videoFiles.Clear();

                var videoExtensions = new[] { ".mp4", ".m4v", ".avi", ".mkv", ".wmv", ".mov" };
                var files = Directory.GetFiles(folderPath)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f)?.ToLower()))
                    .OrderBy(f => f)
                    .ToList();

                // 获取上次播放的文件路径
                var playbackState = recordManager.GetCurrentPlaybackState();
                string lastWatchedPath = playbackState?.CurrentVideoPath;

                foreach (var file in files)
                {
                    string progressText = "";
                    bool isLastWatched = (file == lastWatchedPath);

                    if (MemoryPlaybackCheckBox.IsChecked == true)
                    {
                        var record = recordManager.GetRecord(file);
                        if (record != null && record.Position > 0)
                        {
                            TimeSpan progressTime = TimeSpan.FromSeconds(record.Position);
                            progressText = $" ({progressTime.ToString(@"hh\:mm\:ss")})";
                        }
                    }

                    videoFiles.Add(new VideoFile
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        ProgressText = progressText,
                        IsLastWatched = isLastWatched
                    });
                }

                FileListBox.ItemsSource = videoFiles;
                FileListBox.Items.Refresh();

                // 更新播放状态（只更新文件夹路径）
                if (MemoryPlaybackCheckBox.IsChecked == true)
                {
                    recordManager.UpdatePlaybackState(folderPath, currentVideoPath,
                        MediaPlayer.NaturalDuration.HasTimeSpan ? MediaPlayer.Position.TotalSeconds : 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载文件夹中的视频文件:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectCurrentFileInList(string filePath)
        {
            var videoFile = videoFiles.FirstOrDefault(f => f.FullPath == filePath);
            if (videoFile != null)
            {
                FileListBox.SelectedItem = videoFile;
            }
        }

        private void ShowStatus(string message)
        {
            StatusText.Text = message;
            StatusOverlay.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            StatusOverlay.Visibility = Visibility.Collapsed;
        }

        // 事件处理程序
        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            HideStatus();
            ProgressSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            NormalProgressSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            FullscreenProgressSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            UpdateTimeDisplay();
            UpdateNormalTimeDisplay();
            UpdateFullscreenTimeDisplay();
            progressTimer.Start();
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            PlayPauseButton.Content = "▶️ 播放";
            NormalPlayPauseIcon.Text = "▶";
            FullscreenPlayPauseIcon.Text = "▶";
            isPlaying = false;
            progressTimer.Stop();
            memorySaveTimer.Stop();

            // 播放结束时删除播放记录（从头开始）
            if (!string.IsNullOrEmpty(currentVideoPath))
            {
                recordManager.RemoveRecord(currentVideoPath);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, "", 0);
                }
            }

            // 自动播放下一个文件
            PlayNextVideo();
        }

        private void PlayNextVideo()
        {
            if (videoFiles.Count > 0 && FileListBox.SelectedIndex < videoFiles.Count - 1)
            {
                FileListBox.SelectedIndex++;
                var nextVideo = videoFiles[FileListBox.SelectedIndex];
                LoadVideoFile(nextVideo.FullPath);
            }
            else
            {
                // 播放列表结束，显示欢迎界面
                ShowWelcomeScreen();

                // 清除播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true)
                {
                    recordManager.UpdatePlaybackState("", "", 0);
                }
            }
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ShowStatus($"播放失败: {e.ErrorException.Message}");
            MessageBox.Show($"视频播放失败:\n{e.ErrorException.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void UpdateTimeDisplay()
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                CurrentTimeText.Text = MediaPlayer.Position.ToString(@"hh\:mm\:ss");
                TotalTimeText.Text = MediaPlayer.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
            }
        }

        // 清除记忆播放记录
        private void ClearMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清除所有播放记录和记忆播放状态吗？", "清除播放记录",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                recordManager.ClearAllRecords();
                MessageBox.Show("播放记录和记忆播放状态已清除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新文件列表以更新进度显示
                if (!string.IsNullOrEmpty(currentFolderPath))
                {
                    LoadVideoFilesFromFolder(currentFolderPath);
                }

                // 显示欢迎界面
                ShowWelcomeScreen();
            }
        }

        // 控制按钮事件
        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "视频文件 (*.mp4;*.m4v;*.avi;*.mkv;*.wmv;*.mov)|*.mp4;*.m4v;*.avi;*.mkv;*.wmv;*.mov|所有文件 (*.*)|*.*",
                Title = "选择视频文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadVideoFile(openFileDialog.FileName);

                // 加载同文件夹的其他视频文件
                string folderPath = Path.GetDirectoryName(openFileDialog.FileName);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    LoadVideoFilesFromFolder(folderPath);
                    SelectCurrentFileInList(openFileDialog.FileName);
                }
            }
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // 使用完全限定名称避免命名冲突
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadVideoFilesFromFolder(dialog.SelectedPath);
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListBox.SelectedItem is VideoFile selectedVideo)
            {
                LoadVideoFile(selectedVideo.FullPath);
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
            PlayPauseButton.Content = "▶️ 播放";
            NormalPlayPauseIcon.Text = "▶";
            FullscreenPlayPauseIcon.Text = "▶";
            isPlaying = false;
            memorySaveTimer.Stop();
            ProgressSlider.Value = 0;
            NormalProgressSlider.Value = 0;
            FullscreenProgressSlider.Value = 0;
            UpdateTimeDisplay();
            UpdateNormalTimeDisplay();
            UpdateFullscreenTimeDisplay();
            UpdateProgressDisplay();

            // 停止时保存播放进度
            if (!string.IsNullOrEmpty(currentVideoPath))
            {
                recordManager.UpdateRecord(currentVideoPath, 0);

                // 同时更新播放状态
                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, 0);
                }
            }
        }

        // 进度条事件处理
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!MediaPlayer.NaturalDuration.HasTimeSpan) return;

            if (isDraggingProgress)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
                UpdateTimeDisplay();
                UpdateProgressDisplay();
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击进度条时也更新位置
            var position = e.GetPosition(ProgressSlider);
            var newValue = (position.X / ProgressSlider.ActualWidth) * ProgressSlider.Maximum;
            ProgressSlider.Value = newValue;

            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(newValue);
                UpdateProgressDisplay();

                // 点击跳转后保存播放进度
                if (!string.IsNullOrEmpty(currentVideoPath))
                {
                    recordManager.UpdateRecord(currentVideoPath, newValue);

                    // 同时更新播放状态
                    if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                    {
                        recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, newValue);
                    }
                }
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 不需要额外处理
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MediaPlayer.Source != null && SpeedComboBox.SelectedItem is ComboBoxItem item)
            {
                double speed = double.Parse(item.Tag.ToString());
                MediaPlayer.SpeedRatio = speed;

                // 更新当前倍速索引
                for (int i = 0; i < speedValues.Length; i++)
                {
                    if (Math.Abs(speedValues[i] - speed) < 0.01)
                    {
                        currentSpeedIndex = i;
                        break;
                    }
                }

                // 更新按钮文本
                NormalSpeedButton.Content = speedOptions[currentSpeedIndex];
                FullscreenSpeedButton.Content = speedOptions[currentSpeedIndex];
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MediaPlayer.Volume = VolumeSlider.Value;
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                // 进入全屏
                previousWindowState = WindowState;
                previousWindowStyle = WindowStyle;

                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;

                isFullscreen = true;
            }
            else
            {
                // 退出全屏
                WindowStyle = previousWindowStyle;
                WindowState = previousWindowState;
                ResizeMode = ResizeMode.CanResize;

                isFullscreen = false;
            }
        }

        // 窗口控制
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                ToggleFullscreen();
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        // 键盘快捷键处理
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    // 空格键暂停/播放
                    TogglePlayPause();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (isVideoFullscreen)
                        ToggleVideoFullscreen();
                    else if (isFullscreen)
                        ToggleFullscreen();
                    break;
                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (MediaPlayer.NaturalDuration.HasTimeSpan)
                        MediaPlayer.Position -= TimeSpan.FromSeconds(5);
                    break;
                case Key.Right:
                    if (MediaPlayer.NaturalDuration.HasTimeSpan)
                        MediaPlayer.Position += TimeSpan.FromSeconds(5);
                    break;
                case Key.N:
                    PlayNextVideo();
                    break;
                case Key.P:
                    PlayPreviousVideo();
                    break;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // 新增事件处理方法
        private void ProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        {
            var thumb = ProgressSlider.Template.FindName("Thumb", ProgressSlider) as Thumb;
            if (thumb != null)
            {
                thumb.Opacity = 1;
            }
        }

        private void ProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        {
            var thumb = ProgressSlider.Template.FindName("Thumb", ProgressSlider) as Thumb;
            if (thumb != null && !thumb.IsMouseOver)
            {
                thumb.Opacity = 0;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 关闭时保存当前播放进度
            if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                double currentPosition = MediaPlayer.Position.TotalSeconds;
                recordManager.UpdateRecord(currentVideoPath, currentPosition);

                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, currentPosition);
                }
            }

            MediaPlayer.Close();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 保存当前播放进度和状态
            if (!string.IsNullOrEmpty(currentVideoPath) && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                double currentPosition = MediaPlayer.Position.TotalSeconds;
                recordManager.UpdateRecord(currentVideoPath, currentPosition);

                if (MemoryPlaybackCheckBox.IsChecked == true && !string.IsNullOrEmpty(currentFolderPath))
                {
                    recordManager.UpdatePlaybackState(currentFolderPath, currentVideoPath, currentPosition);
                }
            }

            progressTimer.Stop();
            memorySaveTimer.Stop();
            controlsHideTimer.Stop();
            MediaPlayer.Close();
            base.OnClosing(e);
        }
    }

    // 视频文件类
    public class VideoFile
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string ProgressText { get; set; }
        public bool IsLastWatched { get; set; }
    }
}
