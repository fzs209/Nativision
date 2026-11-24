using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Display;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nativision
{
    public sealed partial class MainWindow : Window
    {
        private readonly List<StorageFile> _playlist = new List<StorageFile>();
        private int _index = -1;
        private StorageFile _pendingSubtitle;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Nativision";

            var mp = Player.MediaPlayer;
            mp.MediaEnded += OnMediaEnded;
            mp.MediaFailed += OnMediaFailed;
            mp.MediaOpened += (s, e) => RunOnUi(UpdateDiag);
            if (mp.PlaybackSession != null)
            {
                mp.PlaybackSession.PlaybackStateChanged += (s, e) => RunOnUi(UpdateDiag);
            }

            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.IsRepeating = true;
            _timer.Tick += (s, e) => UpdateDiag();
            _timer.Start();

            this.Closed += (s, e) =>
            {
                try { _timer.Stop(); } catch { }
                try { Player.MediaPlayer.Dispose(); } catch { }
            };

            if (App.StartupFiles.Count > 0)
            {
                LoadStartupFiles();
            }
            UpdateDiag();
        }

        private async void LoadStartupFiles()
        {
            var vids = new List<StorageFile>();
            foreach (var path in App.StartupFiles)
            {
                try
                {
                    var f = await StorageFile.GetFileFromPathAsync(path);
                    var ext = f.FileType.ToLowerInvariant();
                    if (ext == ".srt" || ext == ".vtt") _pendingSubtitle = f;
                    else vids.Add(f);
                }
                catch { }
            }
            if (vids.Count > 0)
            {
                _playlist.Clear();
                _playlist.AddRange(vids);
                PlayAt(0);
            }
        }

        public async void PlayExternalPath(string path)
        {
            try
            {
                var f = await StorageFile.GetFileFromPathAsync(path);
                _playlist.Clear();
                _playlist.Add(f);
                PlayAt(0);
            }
            catch { }
        }

        // ---------- opening files ----------

        private IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);

        private async void OnOpenFiles(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".m4v");
            picker.FileTypeFilter.Add(".mkv");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
            var files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;
            _playlist.Clear();
            _playlist.AddRange(files);
            PlayAt(0);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            var vids = new List<StorageFile>();
            StorageFile sub = null;
            foreach (var it in items)
            {
                var f = it as StorageFile;
                if (f == null) continue;
                var ext = f.FileType.ToLowerInvariant();
                if (ext == ".srt" || ext == ".vtt") sub = f;
                else vids.Add(f);
            }
            if (sub != null) _pendingSubtitle = sub;
            if (vids.Count > 0)
            {
                _playlist.Clear();
                _playlist.AddRange(vids);
                PlayAt(0);
            }
            else if (sub != null)
            {
                ApplySubtitleToCurrent();
            }
        }

        // ---------- playback ----------

        private async void PlayAt(int i)
        {
            if (i < 0 || i >= _playlist.Count) return;
            _index = i;
            var file = _playlist[i];
            ErrorText.Visibility = Visibility.Collapsed;

            var source = MediaSource.CreateFromStorageFile(file);
            if (_pendingSubtitle != null)
            {
                try
                {
                    var stream = await _pendingSubtitle.OpenReadAsync();
                    var timed = TimedTextSource.CreateFromStream(stream, "zh");
                    timed.Resolved += OnTimedTextResolved;
                    source.ExternalTimedTextSources.Add(timed);
                }
                catch (Exception ex)
                {
                    SubStatus.Text = "字幕加载失败：" + ex.Message;
                }
            }

            var mp = Player.MediaPlayer;
            mp.Source = source;
            ApplySpeed();
            mp.IsLoopingEnabled = LoopToggle.IsChecked == true;
            mp.Play();

            NowPlaying.Text = "正在播放（" + (_index + 1) + "/" + _playlist.Count + "）：" + file.Name;
            SubStatus.Text = _pendingSubtitle != null
                ? "字幕：已挂外挂字幕 " + _pendingSubtitle.Name + "（在播放控件的字幕按钮里选它）"
                : "字幕：无外挂字幕（内封字幕由系统控件的字幕按钮选择）";
            UpdateDiag();
        }

        private void OnTimedTextResolved(TimedTextSource sender, TimedTextSourceResolveResultEventArgs args)
        {
            if (args.Error != null)
            {
                RunOnUi(() => { SubStatus.Text = "字幕解析失败：" + args.Error.ExtendedError.Message; });
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            RunOnUi(() =>
            {
                if (sender.IsLoopingEnabled) return;
                if (_index < _playlist.Count - 1) PlayAt(_index + 1);
            });
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            RunOnUi(() =>
            {
                ErrorText.Text = "播放失败：" + args.Error + " — " + args.ErrorMessage +
                    "。常见原因：MKV 容器系统管线不认、编码缺少系统扩展（HEVC / 杜比视界扩展）、或这个 Profile 系统管线不支持。" +
                    "先拿同一个文件在 Windows Media Player 里确认能播，再判断是不是本程序的问题。";
                ErrorText.Visibility = Visibility.Visible;
                UpdateDiag();
            });
        }

        private void OnPrev(object sender, RoutedEventArgs e)
        {
            if (_index > 0) PlayAt(_index - 1);
        }

        private void OnNext(object sender, RoutedEventArgs e)
        {
            if (_index < _playlist.Count - 1) PlayAt(_index + 1);
        }

        private void ApplySpeed()
        {
            try
            {
                var item = SpeedBox.SelectedItem as ComboBoxItem;
                if (item == null || item.Tag == null) return;
                double rate = double.Parse(item.Tag.ToString(), CultureInfo.InvariantCulture);
                Player.MediaPlayer.PlaybackSession.PlaybackRate = rate;
            }
            catch { }
        }

        private void OnSpeedChanged(object sender, SelectionChangedEventArgs e) => ApplySpeed();

        private void OnLoopChanged(object sender, RoutedEventArgs e)
        {
            try { Player.MediaPlayer.IsLoopingEnabled = LoopToggle.IsChecked == true; } catch { }
        }

        // ---------- subtitles ----------

        private async void OnOpenSubtitle(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".srt");
            picker.FileTypeFilter.Add(".vtt");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
            var f = await picker.PickSingleFileAsync();
            if (f == null) return;
            _pendingSubtitle = f;
            ApplySubtitleToCurrent();
        }

        private void ApplySubtitleToCurrent()
        {
            if (_index < 0)
            {
                SubStatus.Text = "字幕：已选 " + (_pendingSubtitle != null ? _pendingSubtitle.Name : "") + "，打开视频后自动挂上。";
                return;
            }
            var pos = Player.MediaPlayer.PlaybackSession.Position;
            var wasPlaying = Player.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            PlayAt(_index);
            Player.MediaPlayer.PlaybackSession.Position = pos;
            if (!wasPlaying) Player.MediaPlayer.Pause();
        }

        // ---------- keyboard ----------

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var session = Player.MediaPlayer.PlaybackSession;
            if (session == null) return;
            switch (e.Key)
            {
                case VirtualKey.Right:
                    session.Position = session.Position + TimeSpan.FromSeconds(10);
                    e.Handled = true;
                    break;
                case VirtualKey.Left:
                    var back = session.Position - TimeSpan.FromSeconds(10);
                    session.Position = back < TimeSpan.Zero ? TimeSpan.Zero : back;
                    e.Handled = true;
                    break;
                case VirtualKey.N:
                    OnNext(this, null);
                    e.Handled = true;
                    break;
                case VirtualKey.P:
                    OnPrev(this, null);
                    e.Handled = true;
                    break;
            }
        }

        // ---------- diagnostics ----------

        private string BuildDiagText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Nativision 诊断（WinUI 3 桌面版）===");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("播放方式：系统播放框架（Media Foundation 管线），本程序不自解码");
            sb.AppendLine();

            try
            {
                var di = DisplayInformation.GetForCurrentView();
                var ac = di.GetAdvancedColorInfo();
                sb.AppendLine("[显示]");
                sb.AppendLine("当前高级颜色模式：" + ac.CurrentAdvancedColorKind +
                    "（Sdr=普通，Hdr=HDR 输出中，Wcg=广色域）");
                sb.AppendLine("峰值亮度：" + ac.MaxLuminanceInNits.ToString("0") + " nits" +
                    " · 最低亮度：" + ac.MinLuminanceInNits.ToString("0.###") + " nits" +
                    " · 全帧平均峰值：" + ac.MaxAverageFullFrameLuminanceInNits.ToString("0") + " nits");
                sb.AppendLine("说明：系统没有给普通应用提供「已进入杜比视界模式」的直接读数，" +
                    "上面只能证明显示链路处于哪种高级颜色模式。杜比视界是否触发仍看显示器菜单，并和 Media Player 同一画面对照。");
            }
            catch (Exception ex)
            {
                sb.AppendLine("[显示] 读取失败（桌面版此接口可能不可用，不影响播放判断）：" + ex.Message);
            }
            sb.AppendLine();

            try
            {
                var mp = Player.MediaPlayer;
                var s = mp.PlaybackSession;
                sb.AppendLine("[播放]");
                if (_index >= 0 && _index < _playlist.Count)
                    sb.AppendLine("文件：" + _playlist[_index].Name);
                else
                    sb.AppendLine("文件：（无）");
                if (s != null)
                {
                    sb.AppendLine("状态：" + s.PlaybackState);
                    sb.AppendLine("解码分辨率：" + s.NaturalVideoWidth + " × " + s.NaturalVideoHeight +
                        (s.NaturalVideoWidth == 0 ? " — 尚未解出视频" : ""));
                    sb.AppendLine("位置 / 总时长：" + FormatTime(s.Position) + " / " + FormatTime(s.NaturalDuration));
                    sb.AppendLine("倍速：" + s.PlaybackRate.ToString("0.##") + "× · 可定位：" + s.CanSeek);
                    var ranges = s.GetBufferedRanges();
                    if (ranges.Count > 0)
                    {
                        double ahead = 0;
                        foreach (var r in ranges)
                        {
                            if (s.Position >= r.Start && s.Position <= r.End)
                                ahead = (r.End - s.Position).TotalSeconds;
                        }
                        sb.AppendLine("缓冲领先：" + ahead.ToString("0.0") + " 秒");
                    }
                    sb.AppendLine("音轨 / 字幕轨：见播放控件上的音频与字幕按钮（系统管线直接管理）");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("[播放] 读取失败：" + ex.Message);
            }
            return sb.ToString();
        }

        private void UpdateDiag()
        {
            try { DiagText.Text = BuildDiagText(); } catch { }
        }

        private void OnCopyDiag(object sender, RoutedEventArgs e)
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(BuildDiagText());
                Clipboard.SetContent(pkg);
            }
            catch (Exception ex)
            {
                ErrorText.Text = "复制诊断失败：" + ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        // ---------- helpers ----------

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return t.ToString(@"h\:mm\:ss");
            return t.ToString(@"m\:ss");
        }

        private void RunOnUi(Action action)
        {
            if (DispatcherQueue.HasThreadAccess) action();
            else DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
