using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AzureStorageFunctions;

public class TableFunction
{
    private readonly string _connectionString;
    private readonly string _customerTableName;
    private readonly string _productTableName;

    public TableFunction()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("AzureStorage__ConnectionString")
            ?? throw new InvalidOperationException(
                "AzureStorage__ConnectionString is not configured.");

        _customerTableName =
            Environment.GetEnvironmentVariable("AzureStorage__CustomerTable")
            ?? "CustomerProfiles";

        _productTableName =
            Environment.GetEnvironmentVariable("AzureStorage__ProductTable")
            ?? "Products";
    }

    [Function("TableFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<TableRequest>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (request == null)
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "Invalid request body."
                    });
            }

            if (string.IsNullOrWhiteSpace(request.Type))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "Type is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(request.PartitionKey))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "PartitionKey is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(request.RowKey))
            {
                return await CreateResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    new
                    {
                        success = false,
                        message = "RowKey is required."
                    });
            }

            if (request.Type.Equals(
                    "Customer",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName))
                {
                    return await CreateResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        new
                        {
                            success = false,
                            message = "FirstName and LastName are required."
                        });
                }

                var table = new TableClient(
                    _connectionString,
                    _customerTableName);

                await table.CreateIfNotExistsAsync();

                var entity = new TableEntity(
                    request.PartitionKey,
                    request.RowKey)
                {
                    ["FirstName"] = request.FirstName,
                    ["LastName"] = request.LastName,
                    ["Email"] = request.Email ?? string.Empty,
                    ["City"] = request.City ?? string.Empty
                };

                await table.UpsertEntityAsync(entity);

                return await CreateResponse(
                    req,
                    HttpStatusCode.OK,
                    new
                    {
                        success = true,
                        message = "Customer stored successfully in Azure Table Storage.",
                        table = _customerTableName,
                        partitionKey = request.PartitionKey,
                        rowKey = request.RowKey
                    });
            }

            if (request.Type.Equals(
                    "Product",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.ProductName) ||
                    string.IsNullOrWhiteSpace(request.Category))
                {
                    return await CreateResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        new
                        {
                            success = false,
                            message = "ProductName and Category are required."
                        });
                }

                var table = new TableClient(
                    _connectionString,
                    _productTableName);

                await table.CreateIfNotExistsAsync();

                var entity = new TableEntity(
                    request.PartitionKey,
                    request.RowKey)
                {
                    ["ProductName"] = request.ProductName,
                    ["Category"] = request.Category,
                    ["Stock"] = request.Stock
                };

                await table.UpsertEntityAsync(entity);

                return await CreateResponse(
                    req,
                    HttpStatusCode.OK,
                    new
                    {
                        success = true,
                        message = "Product stored successfully in Azure Table Storage.",
                        table = _productTableName,
                        partitionKey = request.PartitionKey,
                        rowKey = request.RowKey
                    });
            }

            return await CreateResponse(
                req,
                HttpStatusCode.BadRequest,
                new
                {
                    success = false,
                    message = "Type must be Customer or Product."
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
                    message = "An error occurred while storing table information.",
                    error = ex.Message
                });
        }
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

    private class TableRequest
    {
        public string Type { get; set; } = string.Empty;
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public int Stock { get; set; }
    }
}