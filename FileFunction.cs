using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AzureStorageFunctions;

public class FileFunction
{
    private readonly ShareClient _share;

    public FileFunction()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "AzureStorage__ConnectionString")
            ?? throw new InvalidOperationException(
                "AzureStorage__ConnectionString is not configured.");

        var shareName =
            Environment.GetEnvironmentVariable(
                "AzureStorage__FileShare")
            ?? "applicationlogs";

        _share =
            new ShareClient(
                connectionString,
                shareName);
    }

    [Function("FileFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post")]
        HttpRequestData req)
    {
        try
        {
            var request =
                await JsonSerializer.DeserializeAsync<FileRequest>(
                    req.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Message))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "A log message is required."
                    });
            }

            await _share.CreateIfNotExistsAsync();

            var directory =
                _share.GetRootDirectoryClient();

            var fileName =
                $"function-log-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log";

            var content =
                $"Application Log{Environment.NewLine}" +
                $"UTC: {DateTime.UtcNow:O}{Environment.NewLine}" +
                $"Message: {request.Message.Trim()}{Environment.NewLine}";

            var bytes =
                Encoding.UTF8.GetBytes(content);

            using var stream =
                new MemoryStream(bytes);

            var file =
                directory.GetFileClient(fileName);

            await file.CreateAsync(
                bytes.Length);

            await file.UploadRangeAsync(
                new HttpRange(
                    0,
                    bytes.Length),
                stream);

            return await CreateResponse(
                req,
                HttpStatusCode.OK,
                new
                {
                    success = true,
                    message =
                        "Log file stored successfully in Azure Files.",
                    share = _share.Name,
                    fileName,
                    size = bytes.Length
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
                    message =
                        "An error occurred while writing to Azure Files.",
                    error = ex.Message
                });
        }
    }

    private static async Task<HttpResponseData> CreateResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        object data)
    {
        var response =
            req.CreateResponse(statusCode);

        response.Headers.Add(
            "Content-Type",
            "application/json");

        await response.WriteStringAsync(
            JsonSerializer.Serialize(data));

        return response;
    }

    private class FileRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}