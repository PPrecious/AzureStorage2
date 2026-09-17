using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AzureStorageFunctions;

public class BlobFunction
{
    private readonly string _connectionString;
    private readonly string _containerName;

    private static readonly HashSet<string> AllowedExtensions =
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

    public BlobFunction()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("AzureStorage__ConnectionString")
            ?? throw new InvalidOperationException(
                "AzureStorage__ConnectionString is not configured.");

        _containerName =
            Environment.GetEnvironmentVariable("AzureStorage__BlobContainer")
            ?? "productmedia";
    }

    [Function("BlobFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypes))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "Content-Type header is required."
                    });
            }

            var contentTypeHeader = contentTypes.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(contentTypeHeader) ||
                !contentTypeHeader.StartsWith(
                    "multipart/form-data",
                    StringComparison.OrdinalIgnoreCase))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "Request must use multipart/form-data."
                    });
            }

            var boundary = GetBoundary(contentTypeHeader);

            if (string.IsNullOrWhiteSpace(boundary))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "Multipart boundary was not found."
                    });
            }

            var form = await MultipartFormDataParser.ParseAsync(
                req.Body,
                boundary);

            if (form.File == null)
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "No file was supplied."
                    });
            }

            var file = form.File;

            if (file.Length == 0)
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "The uploaded file is empty."
                    });
            }

            const long maxBytes = 20 * 1024 * 1024;

            if (file.Length > maxBytes)
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "File size cannot exceed 20 MB."
                    });
            }

            var extension =
                Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "File type is not supported."
                    });
            }

            var safeName = SanitizeFileName(file.FileName);

            var container = new BlobContainerClient(
                _connectionString,
                _containerName);

            await container.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.None);

            var blob = container.GetBlobClient(safeName);

            await blob.UploadAsync(
                file.Content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = GetContentType(
                            safeName,
                            file.ContentType)
                    }
                });

            return await CreateResponse(
                req,
                HttpStatusCode.OK,
                new
                {
                    success = true,
                    message = "File uploaded successfully to Azure Blob Storage.",
                    container = _containerName,
                    blobName = safeName,
                    contentType = GetContentType(
                        safeName,
                        file.ContentType),
                    size = file.Length
                });
        }
        catch (Exception ex)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "An error occurred while uploading the blob.",
                    error = ex.Message
                });
        }
    }

    private static string? GetBoundary(string contentType)
    {
        var parts = contentType.Split(';');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.StartsWith(
                    "boundary=",
                    StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["boundary=".Length..]
                    .Trim('"');
            }
        }

        return null;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);

        foreach (var character in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(character, '_');
        }

        return name;
    }

    private static string GetContentType(
        string fileName,
        string? suppliedContentType)
    {
        if (!string.IsNullOrWhiteSpace(suppliedContentType) &&
            suppliedContentType != "application/octet-stream")
        {
            return suppliedContentType;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };
    }

    private static async Task<HttpResponseData> CreateResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        object data)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");

        await response.WriteStringAsync(
            JsonSerializer.Serialize(data));

        return response;
    }
}

internal static class MultipartFormDataParser
{
    public static async Task<MultipartForm> ParseAsync(
        Stream stream,
        string boundary)
    {
        var memory = new MemoryStream();

        await stream.CopyToAsync(memory);
        memory.Position = 0;

        var content = await new StreamReader(memory).ReadToEndAsync();

        var boundaryMarker = "--" + boundary;

        var sections = content.Split(
            boundaryMarker,
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            var cleaned = section.Trim();

            if (cleaned == "--")
                continue;

            var headerEnd = cleaned.IndexOf(
                "\r\n\r\n",
                StringComparison.Ordinal);

            if (headerEnd < 0)
                continue;

            var headers = cleaned[..headerEnd];
            var body = cleaned[(headerEnd + 4)..];

            var contentDisposition =
                headers.Split(
                    "\r\n",
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x =>
                    x.StartsWith(
                        "Content-Disposition:",
                        StringComparison.OrdinalIgnoreCase));

            if (contentDisposition == null)
                continue;

            if (!contentDisposition.Contains(
                    "filename=",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = ExtractValue(
                contentDisposition,
                "filename");

            var contentTypeLine =
                headers.Split(
                    "\r\n",
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x =>
                    x.StartsWith(
                        "Content-Type:",
                        StringComparison.OrdinalIgnoreCase));

            var contentType =
                contentTypeLine?
                    .Split(':', 2)
                    .ElementAtOrDefault(1)?
                    .Trim()
                ?? "application/octet-stream";

            var bytes = System.Text.Encoding.UTF8.GetBytes(
                body.TrimEnd('\r', '\n', '-'));

            return new MultipartForm
            {
                File = new MultipartFile
                {
                    FileName = fileName,
                    ContentType = contentType,
                    Content = new MemoryStream(bytes),
                    Length = bytes.Length
                }
            };
        }

        return new MultipartForm();
    }

    private static string ExtractValue(
        string input,
        string key)
    {
        var marker = key + "=";
        var index = input.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return string.Empty;

        var value = input[(index + marker.Length)..];

        var semicolon = value.IndexOf(';');

        if (semicolon >= 0)
            value = value[..semicolon];

        return value.Trim().Trim('"');
    }
}

internal class MultipartForm
{
    public MultipartFile? File { get; set; }
}

internal class MultipartFile
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
    public long Length { get; set; }
}