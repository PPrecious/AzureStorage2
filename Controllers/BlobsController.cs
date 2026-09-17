using AzureStorageWebApp.Services;

using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class BlobsController : Controller
{
    private readonly AzureStorageService _storage;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string>
        AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".gif",
                ".webp",
                ".svg",
                ".mp4",
                ".webm",
                ".mov",
                ".mp3",
                ".wav",
                ".m4a"
            };

    public BlobsController(
        AzureStorageService storage,
        IConfiguration configuration)
    {
        _storage = storage;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        await _storage.EnsureResourcesAsync(
            cancellationToken);

        var blobs =
            await _storage.GetBlobsAsync(
                cancellationToken);

        return View(blobs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var maxBytes =
            _configuration.GetValue<long>(
                "AzureStorage:MaxUploadBytes",
                20 * 1024 * 1024);

        if (file is null || file.Length == 0)
        {
            TempData["Error"] =
                "Please select a multimedia file.";

            return RedirectToAction(
                nameof(Index));
        }

        if (file.Length > maxBytes)
        {
            TempData["Error"] =
                "The selected file exceeds " +
                "the 20 MB upload limit.";

            return RedirectToAction(
                nameof(Index));
        }

        var extension =
            Path.GetExtension(
                file.FileName);

        if (!AllowedExtensions.Contains(
                extension))
        {
            TempData["Error"] =
                "File type is not supported.";

            return RedirectToAction(
                nameof(Index));
        }

        var contentType =
            file.ContentType;

        if (string.IsNullOrWhiteSpace(
                contentType) ||
            contentType.Equals(
                "application/octet-stream",
                StringComparison.OrdinalIgnoreCase))
        {
            contentType =
                AzureStorageService.GetContentType(
                    file.FileName);
        }

        await using var stream =
            file.OpenReadStream();

        await _storage.UploadBlobAsync(
            file.FileName,
            stream,
            contentType,
            cancellationToken);

        TempData["Success"] =
            "Multimedia file uploaded successfully " +
            "to Azure Blob Storage.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ViewBlob(
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(
                "Blob name is required.");
        }

        var result =
            await _storage.DownloadBlobAsync(
                name,
                cancellationToken);

        return File(
            result.Stream,
            result.ContentType);
    }

    [HttpGet]
    public async Task<IActionResult> Download(
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(
                "Blob name is required.");
        }

        var result =
            await _storage.DownloadBlobAsync(
                name,
                cancellationToken);

        var safeName =
            AzureStorageService.SanitizeFileName(
                name);

        return File(
            result.Stream,
            result.ContentType,
            safeName);
    }
}