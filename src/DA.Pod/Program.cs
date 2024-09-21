// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ConsoleAppFramework;
using DA.Pod;
using Downloader;
using Sagara.FeedReader;
using Sagara.FeedReader.Feeds;

var app = ConsoleApp.Create();
app.Add<AppCommands>();
app.Run(args);

/// <summary>
/// App Commands.
/// </summary>
#pragma warning disable SA1649 // File name should match first type name
public class AppCommands
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Debug command.
    /// </summary>
    /// <param name="url">Url to download.</param>
    /// <param name="outputDirectory">-o, Output Directory.</param>
    /// <param name="verbose">-v, Verbose logging.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    [Command("download")]
    public async Task DownloadCommandAsync([Argument] string url, string? outputDirectory = default, bool verbose = false, CancellationToken cancellationToken = default)
    {
        outputDirectory ??= Directory.GetCurrentDirectory();
        var consoleLog = new ConsoleLog(verbose);
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "DA.Pod");
        consoleLog.Log($"Downloading {url}");
        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            consoleLog.LogError($"Failed to download {url}");
            return;
        }

        var rssString = await response.Content.ReadAsStringAsync();
        var feed = FeedReader.ReadFromString(rssString);
        if (feed == null)
        {
            consoleLog.LogError($"Failed to parse {url}");
            return;
        }

        var feedTitle = feed.Title;
        if (string.IsNullOrWhiteSpace(feedTitle))
        {
            consoleLog.LogError($"Failed to get feed title for {url}");
            return;
        }

        consoleLog.Log($"Feed Title: {feedTitle}");

        outputDirectory = Path.Combine(outputDirectory, MakeValidFileName(feedTitle));
       
        var dirInfo = Directory.CreateDirectory(outputDirectory);
        if (!dirInfo.Exists)
        {
            consoleLog.LogError($"Failed to create directory {outputDirectory}");
            return;
        }

        var downloader = new DownloadService(new DownloadConfiguration()
        {
            ChunkCount = 8,
            ParallelDownload = true,
        });

        downloader.DownloadFileCompleted += (sender, e) =>
        {
            if (e.Error != null)
            {
                consoleLog.LogError($"Download failed: {e.Error.Message}");
            }
            else if (e.Cancelled)
            {
                consoleLog.LogWarning($"Download cancelled");
            }
            else if (e.UserState is Downloader.DownloadPackage package)
            {
                consoleLog.Log($"Downloaded {package.FileName}");
            }
        };

        consoleLog.Log($"Output Directory: {outputDirectory}");
        var reverseFeed = feed.Items.Reverse();
        foreach (var item in reverseFeed)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                consoleLog.LogWarning("Download cancelled.");
                return;
            }

            var title = item.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                consoleLog.LogError($"Failed to get title for item in {url}");
                continue;
            }

            var fileName = MakeValidFileName(title);

            if (item.SpecificItem is MediaRssFeedItem mediaItem)
            {
                if (mediaItem.Enclosure?.Url == null)
                {
                    consoleLog.LogWarning($"Item has no enclosure: {title}");
                    continue;
                }

                var extenstion = mediaItem.Enclosure.MediaType switch
                {
                    "audio/mpeg" => ".mp3",
                    "audio/x-m4a" => ".m4a",
                    "audio/x-wav" => ".wav",
                    "audio/ogg" => ".ogg",
                    "audio/flac" => ".flac",
                    "audio/aac" => ".aac",
                    "audio/x-ms-wma" => ".wma",
                    "audio/x-ms-wax" => ".wax",
                    "audio/x-ms-wmv" => ".wmv",
                    _ => ".bin",
                };

                fileName += extenstion;

                var fileOutputPath = Path.Combine(outputDirectory, fileName);

                if (File.Exists(fileOutputPath))
                {
                    consoleLog.LogWarning($"File already exists: {title}");
                    continue;
                }

                consoleLog.Log($"Downloading {title}");
                await downloader.DownloadFileTaskAsync(mediaItem.Enclosure.Url, fileOutputPath, cancellationToken);

                if (!File.Exists(fileOutputPath))
                {
                    consoleLog.LogError($"Failed to download {title}");
                    continue;
                }

                // Change file date to the published date.
                if (item.PublishingDate != null)
                {
                    File.SetCreationTime(fileOutputPath, item.PublishingDate.Value);
                    File.SetLastWriteTime(fileOutputPath, item.PublishingDate.Value);
                }
            }
            else
            {
                consoleLog.LogWarning($"Item is not a media item: {title}");
                continue;
            }
        }
    }

    private string MakeValidFileName(string input)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string validName = Regex.Replace(input, $"[{Regex.Escape(new string(invalidChars))}]", "_");
        validName = validName.Trim();
        if (validName.Length > 255)
        {
            validName = validName.Substring(0, 255);
        }

        return validName;
    }

    private bool CanWriteFileName(string fileName)
    {
        string tempPath = Path.GetTempPath();
        string tempFile = Path.Combine(tempPath, fileName);

        try
        {
            using (FileStream fs = File.Create(tempFile))
            {
                fs.Close();
            }

            File.Delete(tempFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}