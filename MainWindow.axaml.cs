using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;

namespace DingTextureTool;

public partial class MainWindow : Avalonia.Controls.Window
{
    private enum LogType
    {
        Info,
        Warn,
        Error,
    }
    private enum ExecuteTypeEn
    {
        RemoveWatermark,
        DownSample,
    }
    private static readonly Dictionary<ExecuteTypeEn, string> ExecuteTypeDic = new()
    {
        {ExecuteTypeEn.RemoveWatermark, "清除水印"},
        {ExecuteTypeEn.DownSample, "降采样"},
    };

    private string? texturePath, folderPath, outputPath;
    private OpenCvSharp.Rect watermarkRect;
    private ushort downSampleSize;
    private ExecuteTypeEn executeType;
    
    public MainWindow()
    {
        InitializeComponent();

        watermarkRect = new Rect(91,97,8,2);
        downSampleSize = 512;
        executeType = ExecuteTypeEn.DownSample;
        LogListBox.Items.Clear();
        
        RefreshImg();
        RefreshWatermarkRect();
        RefreshDownsampleSize();
        RefreshComboBox();
    }

    private void RefreshImg()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "images", "teamLogo.png");
        var bitmap = new Bitmap(iconPath);
        TeamLogoImg.Source = bitmap;
    }
    private void RefreshWatermarkRect()
    {
        WaterMarkXText.Text = watermarkRect.X.ToString();
        WaterMarkYText.Text = watermarkRect.Y.ToString();
        WaterMarkWidthText.Text = watermarkRect.Width.ToString();
        WaterMarkHeightText.Text = watermarkRect.Height.ToString();
    }
    private void RefreshDownsampleSize() => MaxSizeText.Text = downSampleSize.ToString();
    private void RefreshComboBox()
    {
        ExecuteBox.Items.Clear();
        foreach (var kv in ExecuteTypeDic) ExecuteBox.Items.Add(new ComboBoxItem { Content = kv.Value });
        
        ExecuteBox.SelectedIndex = (int)executeType;
    }

    private int AddLogItem(string logStr, LogType logType)
    {
        return LogListBox.Items.Add(new ListBoxItem
        {
            Content = logStr,
            Foreground = logType == LogType.Error ? Brushes.Red : logType == LogType.Warn ? Brushes.Orange : Brushes.Black,
        });
    }
    private void MoveLogListScroll(int idx = -1)
    {
        if (idx == -1)
        {
            MoveLogListScroll(LogListBox.Items.Count - 1);
            return;
        }

        if (idx < 0 || idx >= LogListBox.Items.Count) return;

        Dispatcher.UIThread.Post(() => LogListBox.ScrollIntoView(idx));
    }

    private async Task<string?> SelectFile(string desc, FilePickerFileType filter)
    {
        var fileDialog = new FilePickerOpenOptions
        {
            Title = desc,
            AllowMultiple = false,
            FileTypeFilter  = [filter]
        };
        
        var list = await StorageProvider.OpenFilePickerAsync(fileDialog);
        if (list.Count == 0) return null;
        
        return list[0].Path.LocalPath;
    }
    private async Task<string?> SelectFolder(string desc)
    {
        var folderDialog = new FolderPickerOpenOptions
        {
            Title = desc,
            AllowMultiple = false
        };

        var list = await StorageProvider.OpenFolderPickerAsync(folderDialog);
        if (list.Count == 0) return null;
        
        return list[0].Path.LocalPath;
    }
    
    private void OnTexturePathChanged(object? sender, TextChangedEventArgs e) => texturePath = TexturePathText.Text;
    private void OnTextureFolderPathChanged(object? sender, TextChangedEventArgs e) => folderPath = FolderPathText.Text;
    private void OnOutputPathChanged(object? sender, TextChangedEventArgs e) => outputPath = OutPutFolderPathText.Text;
    private void OnDownSampleSizeChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(MaxSizeText.Text))
        {
            downSampleSize = 0;
            return;
        }

        if (!ushort.TryParse(MaxSizeText.Text, out ushort size))
        {
            AddLogItem($"降采样尺寸，只能是正整数", LogType.Error);
            MoveLogListScroll();
            MaxSizeText.Text = downSampleSize.ToString();
            return;
        }

        downSampleSize = size;
    }
    
    private void OnWatermarkCoordChanged(TextBox? sourceTextBox, Func<OpenCvSharp.Rect, ushort, OpenCvSharp.Rect> updateRect, Func<OpenCvSharp.Rect, string> getFallback)
    {
        if (sourceTextBox == null)
            return;

        if (string.IsNullOrEmpty(sourceTextBox.Text))
        {
            watermarkRect = updateRect(watermarkRect, 0);
            return;
        }

        if (!ushort.TryParse(sourceTextBox.Text, out ushort value))
        {
            AddLogItem($"水印位置 只能是正整数", LogType.Error);
            MoveLogListScroll();
            sourceTextBox.Text = getFallback(watermarkRect);
            return;
        }

        watermarkRect = updateRect(watermarkRect, value);
    }
    private void OnWatermarkXChanged(object? sender, TextChangedEventArgs e) => OnWatermarkCoordChanged(WaterMarkXText, (rect, val) => new OpenCvSharp.Rect(val, rect.Y, rect.Width, rect.Height), rect => rect.X.ToString());
    private void OnWatermarkYChanged(object? sender, TextChangedEventArgs e) => OnWatermarkCoordChanged(WaterMarkYText, (rect, val) => new OpenCvSharp.Rect(rect.X, val, rect.Width, rect.Height), rect => rect.Y.ToString());
    private void OnWatermarkWidthChanged(object? sender, TextChangedEventArgs e) => OnWatermarkCoordChanged(WaterMarkWidthText, (rect, val) => new OpenCvSharp.Rect(rect.X, rect.Y, val, rect.Height), rect => rect.Width.ToString());
    private void OnWatermarkHeightChanged(object? sender, TextChangedEventArgs e) => OnWatermarkCoordChanged(WaterMarkHeightText, (rect, val) => new OpenCvSharp.Rect(rect.X, rect.Y, rect.Width, val), rect => rect.Height.ToString());

    private void OnExecuteTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        executeType = (ExecuteTypeEn)ExecuteBox.SelectedIndex;
        
        WatermarkLabel.IsVisible = executeType == ExecuteTypeEn.RemoveWatermark;
        WatermarkRect.IsVisible = executeType == ExecuteTypeEn.RemoveWatermark;
        DownSampleLabel.IsVisible = executeType == ExecuteTypeEn.DownSample;
        MaxSizeText.IsVisible = executeType == ExecuteTypeEn.DownSample;
    }

    private async void OnTextureSelectBtn(object? sender, RoutedEventArgs e) => TexturePathText.Text = texturePath = await SelectFile("选择图片", new FilePickerFileType("图片"){ Patterns = ["*.png", "*.jpg"] });
    private async void OnTextureFolderSelectBtn(object? sender, RoutedEventArgs e) => FolderPathText.Text = folderPath = await SelectFolder("选择图片文件夹");
    private async void OnOutputFolderBtn(object? sender, RoutedEventArgs e) => OutPutFolderPathText.Text = outputPath = await SelectFolder("选择输出文件夹");
    private void OnExcuteBtn(object? sender, RoutedEventArgs e)
    {
        switch (executeType)
        {
            case ExecuteTypeEn.RemoveWatermark:
                RemoveWatermark();
                break;
            case ExecuteTypeEn.DownSample:
                DownSample();
                break;
        }
    }
    
    public void RemoveWatermark(string inputFilePath, string outputFilePath, OpenCvSharp.Rect _watermarkRect)
    {
        if (string.IsNullOrEmpty(outputFilePath))
        {
            AddLogItem($"[去水印] 目标输出文件路径是空的", LogType.Error);
            return;
        }

        Mat src = Cv2.ImRead(inputFilePath);

        OpenCvSharp.Size srcSize = src.Size();
        int x = (int)(_watermarkRect.X * srcSize.Width * 0.01f);
        int y = (int)(srcSize.Height - (srcSize.Width * (1 - _watermarkRect.Y * 0.01f)));
        int w = (int)(_watermarkRect.Width * srcSize.Width * 0.01f);
        int h = (int)(_watermarkRect.Height * srcSize.Width * 0.01f);
        _watermarkRect = new(x,  y, w, h);
        
        Mat mask = new Mat(srcSize, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mask, _watermarkRect, Scalar.White, -1);

        Mat result = new Mat();
        Cv2.Inpaint(src, mask, result, 1f, InpaintMethod.Telea);

        var blurredRect = new OpenCvSharp.Rect(_watermarkRect.X - 2, _watermarkRect.Y - 2, _watermarkRect.Width + 4, _watermarkRect.Height + 4);
        Mat blurRegion = new Mat(result, blurredRect);
        Cv2.GaussianBlur(blurRegion, blurRegion, new OpenCvSharp.Size(21, 21), 0);
        
        string? outputDirPath = Path.GetDirectoryName(outputFilePath);
        if (!Directory.Exists(outputDirPath)) Directory.CreateDirectory(outputDirPath);
        Cv2.ImWrite(outputFilePath, result);
    }
    private void RemoveWatermark()
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            AddLogItem($"【去水印】图片没有输出路径", LogType.Error);
            MoveLogListScroll();
            return;
        }

        if (!string.IsNullOrEmpty(texturePath))
        {
            string fileName = Path.GetFileName(texturePath);
            string targetPath = Path.Combine(outputPath, fileName);
            RemoveWatermark(texturePath, targetPath, watermarkRect);
            AddLogItem($"【去水印】成功：{fileName}", LogType.Info);
        }

        if (!string.IsNullOrEmpty(folderPath))
        {
            foreach (string filePath in Directory.GetFiles(folderPath, "*.*",SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (extension != ".png" &&  extension != ".jpg") continue;
            
                string fileName = Path.GetFileName(filePath);
                string relativePath = Path.GetRelativePath(folderPath, filePath);
                string targetPath = Path.Combine(outputPath, relativePath);
            
                RemoveWatermark(filePath, targetPath, watermarkRect);
            
                AddLogItem($"【去水印】成功：{fileName}", LogType.Info);
            }
        }

        MoveLogListScroll();
    }

    private void DownSample(string inputFilePath, string outputFilePath, ushort size)
    {
        if (string.IsNullOrEmpty(outputFilePath))
        {
            AddLogItem($"[降采样] 目标输出文件路径是空的", LogType.Error);
            return;
        }

        string fileName = Path.GetFileName(inputFilePath);
        Mat src = Cv2.ImRead(inputFilePath);

        int maxSide = Math.Max(src.Width, src.Height);
        if (maxSide <= size)
        {
            AddLogItem($"【降采样】图片：{fileName}, 尺寸已经小于采样值：{src.Size()} -> {size}", LogType.Warn);
            MoveLogListScroll();
            return;
        }

        float scale = (float)size / maxSide;
        int newWidth = (int)Math.Ceiling(src.Width * scale);
        int newHeight = (int)Math.Ceiling(src.Height * scale);

        Mat dst = new Mat();
        Cv2.Resize(src, dst, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Lanczos4);

        string? outputDirPath = Path.GetDirectoryName(outputFilePath);
        if (!Directory.Exists(outputDirPath)) Directory.CreateDirectory(outputDirPath);
        Cv2.ImWrite(outputFilePath, dst);
        
        AddLogItem($"【降采样】成功：{fileName}; {src.Size()} -> {size}", LogType.Info);
    }
    private void DownSample()
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            AddLogItem($"【降采样】图片没有输出路径", LogType.Error);
            MoveLogListScroll();
            return;
        }
        
        if (!string.IsNullOrEmpty(texturePath))
        {
            string fileName = Path.GetFileName(texturePath);
            string targetPath = Path.Combine(outputPath, fileName);
            DownSample(texturePath, targetPath, downSampleSize);
        }

        if (!string.IsNullOrEmpty(folderPath))
        {
            foreach (string filePath in Directory.GetFiles(folderPath, "*.*",SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (extension != ".png" &&  extension != ".jpg") continue;
            
                string relativePath = Path.GetRelativePath(folderPath, filePath);
                string targetPath = Path.Combine(outputPath, relativePath);
            
                DownSample(filePath, targetPath, downSampleSize);
            }
        }

        MoveLogListScroll();
    }
}