using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WhatCanIDelete.Models;

namespace WhatCanIDelete.Services
{
    /// <summary>
    /// Performs file inspection for a target directory without modifying any files.
    /// </summary>
    public class FileAnalyzer
    {
        private static readonly TimeSpan OldAccessThreshold = TimeSpan.FromDays(365);
        private static readonly TimeSpan MediumAccessThreshold = TimeSpan.FromDays(180);
        private static readonly long LargeFileThreshold = 100L * 1024 * 1024; // 100 MB
        private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".tmp",
            ".log",
            ".bak",
            ".old",
            ".cache"
        };

        public Task<IReadOnlyList<FileAnalysisResult>> AnalyzeFolderAsync(string rootFolder, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                throw new ArgumentException("Root folder is required", nameof(rootFolder));
            }

            return Task.Run(() => AnalyzeFolder(rootFolder, cancellationToken), cancellationToken);
        }

        private IReadOnlyList<FileAnalysisResult> AnalyzeFolder(string rootFolder, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var results = new List<FileAnalysisResult>();
            var folders = new Stack<string>();
            folders.Push(rootFolder);

            while (folders.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var current = folders.Pop();
                try
                {
                    foreach (var sub in Directory.GetDirectories(current))
                    {
                        folders.Push(sub);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var info = new FileInfo(file);
                        var lastAccess = info.LastAccessTime;
                        var hasAccess = lastAccess != DateTime.MinValue && lastAccess != DateTime.MaxValue;

                        var result = new FileAnalysisResult
                        {
                            FileName = info.Name,
                            FullPath = info.FullName,
                            SizeInBytes = info.Length,
                            LastModified = info.LastWriteTime,
                            LastAccessed = hasAccess ? info.LastAccessTime : null,
                            Extension = info.Extension
                        };
                        Classify(result, now);
                        results.Add(result);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (PathTooLongException)
                    {
                        continue;
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                }
            }

            EnrichDuplicates(results);
            return results;
        }

        private void Classify(FileAnalysisResult candidate, DateTime currentTime)
        {
            if (TemporaryExtensions.Contains(candidate.Extension))
            {
                candidate.Category = FileCategory.LikelySafe;
                candidate.Reason = "Temporary or cache-style extension.";
                return;
            }

            var lastAccess = candidate.LastAccessed ?? candidate.LastModified;
            var age = currentTime - lastAccess;

            if (age >= OldAccessThreshold)
            {
                candidate.Category = FileCategory.LikelySafe;
                candidate.Reason = "Not accessed for over a year.";
                return;
            }

            if (age >= MediumAccessThreshold && candidate.SizeInBytes >= LargeFileThreshold)
            {
                candidate.Category = FileCategory.BeCareful;
                candidate.Reason = "Large file not opened in over six months.";
                return;
            }

            candidate.Category = FileCategory.DoNotDelete;
            candidate.Reason = "Recent activity or insufficient data.";
        }

        private void EnrichDuplicates(List<FileAnalysisResult> results)
        {
            var duplicates = results
                .GroupBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicates)
            {
                var ordered = group.OrderByDescending(r => r.LastModified).ToList();
                for (var index = 1; index < ordered.Count; index++)
                {
                    var result = ordered[index];
                    result.Category = FileCategory.BeCareful;
                    result.Reason = string.IsNullOrWhiteSpace(result.Reason)
                        ? "Older copy of a duplicate file name."
                        : result.Reason + " Older copy of a duplicate file name.";
                }
            }
        }
    }
}
