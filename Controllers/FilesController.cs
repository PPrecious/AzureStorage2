using Azure;
using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;

using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class FilesController : Controller
{
    private readonly AzureStorageService _storage;

    public FilesController(
        AzureStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);

            var files =
                await _storage.GetLogFilesAsync(
                    cancellationToken);

            return View(files);
        }
        catch (Exception ex)
        {
            TempData["Error"] =
                "Unable to connect to Azure Files: " +
                ex.Message;

            return View(
                new List<LogFileViewModel>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLog(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] =
                "Enter a log message.";

            return RedirectToAction(
                nameof(Index));
        }

        var now =
            DateTime.UtcNow;

        var fileName =
            $"web-log-{now:yyyyMMdd-HHmmssfff}.log";

        var content =
            $"Application Log{Environment.NewLine}" +
            $"UTC: {now:O}{Environment.NewLine}" +
            $"Message: {message.Trim()}{Environment.NewLine}";

        try
        {
            await _storage.UploadLogAsync(
                fileName,
                content,
                cancellationToken);

            TempData["Success"] =
                "Log file stored successfully in Azure Files.";
        }
        catch (Exception ex)
        {
            TempData["Error"] =
                "Unable to store the log file: " +
                ex.Message;
        }

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(
                "File name is required.");
        }

        var safeName =
            AzureStorageService.SanitizeFileName(
                name);

        try
        {
            var stream =
                await _storage.DownloadLogAsync(
                    safeName,
                    cancellationToken);

            return File(
                stream,
                "text/plain",
                safeName);
        }
        catch (RequestFailedException ex)
            when (ex.Status == 404)
        {
            return NotFound(
                "The requested log file was not found.");
        }
        catch (Exception ex)
        {
            TempData["Error"] =
                "Unable to download the log file: " +
                ex.Message;

            return RedirectToAction(
                nameof(Index));
        }
    }
}