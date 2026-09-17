using System.Net;
using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AzureStorageFunctions;

public class QueueFunction
{
    private readonly QueueClient _queue;

    public QueueFunction()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "AzureStorage__ConnectionString")
            ?? throw new InvalidOperationException(
                "AzureStorage__ConnectionString is not configured.");

        var queueName =
            Environment.GetEnvironmentVariable(
                "AzureStorage__QueueName")
            ?? "orderprocessing";

        var options = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None
        };

        _queue = new QueueClient(
            connectionString,
            queueName,
            options);
    }

    [Function("QueueFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "get",
            "post")]
        HttpRequestData req)
    {
        try
        {
            await _queue.CreateIfNotExistsAsync();

            if (req.Method.Equals(
                    "POST",
                    StringComparison.OrdinalIgnoreCase))
            {
                return await SendMessage(req);
            }

            return await ReadMessages(req);
        }
        catch (Exception ex)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "An error occurred while accessing Azure Queue Storage.",
                    error = ex.Message
                });
        }
    }

    private async Task<HttpResponseData> SendMessage(
        HttpRequestData req)
    {
        var request =
            await JsonSerializer.DeserializeAsync<QueueRequest>(
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
                    message = "A queue message is required."
                });
        }

        var type =
            string.IsNullOrWhiteSpace(request.Type)
                ? "Order"
                : request.Type.Trim();

        var message = request.Message.Trim();

        await _queue.SendMessageAsync(message);

        return await CreateResponse(
            req,
            HttpStatusCode.OK,
            new
            {
                success = true,
                message = "Transaction message written to Azure Queue Storage.",
                type,
                queue = _queue.Name,
                queueMessage = message
            });
    }

    private async Task<HttpResponseData> ReadMessages(
        HttpRequestData req)
    {
        var properties =
            await _queue.GetPropertiesAsync();

        var response =
            await _queue.PeekMessagesAsync(
                maxMessages: 32);

        var messages = response.Value
            .Select(message => new
            {
                messageId = message.MessageId,
                messageText = message.MessageText
            })
            .ToList();

        return await CreateResponse(
            req,
            HttpStatusCode.OK,
            new
            {
                success = true,
                queue = _queue.Name,
                approximateMessageCount =
                    properties.Value.ApproximateMessagesCount,
                messages
            });
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

    private class QueueRequest
    {
        public string Type { get; set; } = "Order";
        public string Message { get; set; } = string.Empty;
    }
}