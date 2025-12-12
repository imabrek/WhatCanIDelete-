using System;

namespace WhatCanIDelete.Models
{
    public class FileAnalysisResult
    {
        public string FileName { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public long SizeInBytes { get; init; }
        public DateTime LastModified { get; init; }
        public DateTime? LastAccessed { get; init; }
        public string Extension { get; init; } = string.Empty;
        public FileCategory Category { get; set; }
        public string Reason { get; set; } = string.Empty;

        public string SizeDescription => SizeInBytes switch
        {
            < 1024 => $"{SizeInBytes} B",
            < 1_048_576 => $"{SizeInBytes / 1024.0:F1} KB",
            < 1_073_741_824 => $"{SizeInBytes / 1_048_576.0:F1} MB",
            _ => $"{SizeInBytes / 1_073_741_824.0:F1} GB"
        };

        public string LastAccessedDescription => LastAccessed?.ToString("yyyy-MM-dd") ?? "Unavailable";
        public string CategoryDescription => Category switch
        {
            FileCategory.LikelySafe => "Likely Safe to Delete",
            FileCategory.BeCareful => "Be Careful",
            FileCategory.DoNotDelete => "Do Not Delete",
            _ => "Unknown"
        };
    }
}
