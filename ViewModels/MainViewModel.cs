using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WhatCanIDelete.Models;
using WhatCanIDelete.Services;

namespace WhatCanIDelete.ViewModels
{
    /// <summary>
    /// Bridges the UI with the analyzer logic so the window stays thin.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FileAnalyzer _analyzer = new();
        private string _selectedFolder = string.Empty;
        private string _summaryText = "Select a folder to begin analyzing.";
        private string _statusMessage = string.Empty;
        private bool _isAnalyzing;

        public ObservableCollection<FileAnalysisResult> AnalyzedFiles { get; } = new();

        public RelayCommand SelectFolderCommand { get; }
        public RelayCommand ExportReportCommand { get; }

        public string SelectedFolder
        {
            get => _selectedFolder;
            private set => SetProperty(ref _selectedFolder, value);
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool HasResults => AnalyzedFiles.Count > 0;

        public MainViewModel()
        {
            SelectFolderCommand = new RelayCommand(_ => _ = PickFolderAsync(), _ => !_isAnalyzing);
            ExportReportCommand = new RelayCommand(_ => _ = ExportReportAsync(), _ => HasResults && !_isAnalyzing);
            AnalyzedFiles.CollectionChanged += OnFilesCollectionChanged;
        }

        private async Task PickFolderAsync()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder to scan",
                ShowNewFolderButton = false
            };

            var dialogResult = dialog.ShowDialog();
            if (dialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            await AnalyzeFolderAsync(dialog.SelectedPath);
        }

        private async Task AnalyzeFolderAsync(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            try
            {
                _isAnalyzing = true;
                RaiseCommandCanExecute();
                StatusMessage = "Scanning, please wait...";
                SelectedFolder = folder;

                var results = await _analyzer.AnalyzeFolderAsync(folder);
                AnalyzedFiles.Clear();
                foreach (var result in results.OrderByDescending(r => r.SizeInBytes))
                {
                    AnalyzedFiles.Add(result);
                }

                SummaryText = BuildSummary();
                StatusMessage = $"Packed {AnalyzedFiles.Count} entries from {folder}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to analyze folder: {ex.Message}";
            }
            finally
            {
                _isAnalyzing = false;
                RaiseCommandCanExecute();
            }
        }

        private async Task ExportReportAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export analysis report",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                FileName = "WhatCanIDelete_Report"
            };

            var dialogResult = dialog.ShowDialog();
            if (dialogResult != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            var reportLines = new[] { "File,Size,LastAccessed,Category,Reason" }
                .Concat(AnalyzedFiles.Select(item =>
                    $"\"{item.FileName.Replace("\"", "\"\"")}\",{item.SizeInBytes}," +
                    $"{item.LastAccessedDescription},{item.CategoryDescription},\"{item.Reason.Replace("\"", "\"\"")}\""));

            await File.WriteAllLinesAsync(dialog.FileName, reportLines);
            StatusMessage = $"Report exported to {dialog.FileName}.";
        }

        private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasResults));
            SummaryText = BuildSummary();
            RaiseCommandCanExecute();
        }

        private string BuildSummary()
        {
            var safeCount = AnalyzedFiles.Count(r => r.Category == FileCategory.LikelySafe);
            var carefulCount = AnalyzedFiles.Count(r => r.Category == FileCategory.BeCareful);
            var protectCount = AnalyzedFiles.Count(r => r.Category == FileCategory.DoNotDelete);

            return $"{safeCount} files likely safe to delete, {carefulCount} need caution, {protectCount} should be preserved.";
        }

        private void RaiseCommandCanExecute()
        {
            SelectFolderCommand.RaiseCanExecuteChanged();
            ExportReportCommand.RaiseCanExecuteChanged();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string? propertyName)
        {
            if (propertyName is null)
            {
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
