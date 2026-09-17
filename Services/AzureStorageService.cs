using System.Text;
using System.Text.Json;

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Queues;

using AzureStorageWebApp.Models;

namespace AzureStorageWebApp.Services;

public class AzureStorageService
{
    private readonly IConfiguration _config;

    private readonly TableClient _customerTable;
    private readonly TableClient _productTable;

    private readonly BlobContainerClient _blobContainer;

    private readonly QueueClient _queue;

    private readonly ShareClient _fileShareClient;

    public AzureStorageService(
        IConfiguration config)
    {
        _config = config;

        var connectionString =
            config["AzureStorage:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is missing. " +
                "Configure AzureStorage:ConnectionString " +
                "using User Secrets locally or " +
                "AzureStorage__ConnectionString in Azure App Service.");
        }

        _customerTable =
            new TableClient(
                connectionString,
                config["AzureStorage:CustomerTable"]
                    ?? "CustomerProfiles");

        _productTable =
            new TableClient(
                connectionString,
                config["AzureStorage:ProductTable"]
                    ?? "Products");

        _blobContainer =
            new BlobContainerClient(
                connectionString,
                config["AzureStorage:BlobContainer"]
                    ?? "productmedia");

        var queueOptions =
            new QueueClientOptions
            {
                MessageEncoding =
                    QueueMessageEncoding.None
            };

        _queue =
            new QueueClient(
                connectionString,
                config["AzureStorage:QueueName"]
                    ?? "orderprocessing",
                queueOptions);

        var shareService =
            new ShareServiceClient(
                connectionString);

        _fileShareClient =
            shareService.GetShareClient(
                config["AzureStorage:FileShare"]
                    ?? "applicationlogs");
    }

    public async Task EnsureResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        await _customerTable.CreateIfNotExistsAsync(
            cancellationToken);

        await _productTable.CreateIfNotExistsAsync(
            cancellationToken);

        await _blobContainer.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);

        await _queue.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);

        await _fileShareClient.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);
    }

    public async Task<List<CustomerEntity>>
        GetCustomersAsync(
            CancellationToken cancellationToken = default)
    {
        var customers =
            new List<CustomerEntity>();

        await foreach (
            var customer
            in _customerTable.QueryAsync<CustomerEntity>(
                cancellationToken:
                    cancellationToken))
        {
            customers.Add(customer);
        }

        return customers
            .OrderBy(x => x.RowKey)
            .ToList();
    }

    public async Task AddCustomerAsync(
        CustomerEntity customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            customer);

        await _customerTable.AddEntityAsync(
            customer,
            cancellationToken);
    }

    public async Task UpdateCustomerAsync(
        CustomerEntity customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            customer);

        await _customerTable.UpsertEntityAsync(
            customer,
            TableUpdateMode.Replace,
            cancellationToken);
    }

    public async Task DeleteCustomerAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTableKeys(
            partitionKey,
            rowKey);

        await _customerTable.DeleteEntityAsync(
            partitionKey,
            rowKey,
            cancellationToken:
                cancellationToken);
    }

    public async Task<List<ProductEntity>>
        GetProductsAsync(
            CancellationToken cancellationToken = default)
    {
        var products =
            new List<ProductEntity>();

        await foreach (
            var product
            in _productTable.QueryAsync<ProductEntity>(
                cancellationToken:
                    cancellationToken))
        {
            products.Add(product);
        }

        return products
            .OrderBy(x => x.ProductName)
            .ToList();
    }

    public async Task AddProductAsync(
        ProductEntity product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        await _productTable.AddEntityAsync(
            product,
            cancellationToken);
    }

    public async Task UpdateProductAsync(
        ProductEntity product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        await _productTable.UpsertEntityAsync(
            product,
            TableUpdateMode.Replace,
            cancellationToken);
    }

    public async Task DeleteProductAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTableKeys(
            partitionKey,
            rowKey);

        await _productTable.DeleteEntityAsync(
            partitionKey,
            rowKey,
            cancellationToken:
                cancellationToken);
    }

    public async Task<List<BlobItemViewModel>>
        GetBlobsAsync(
            CancellationToken cancellationToken = default)
    {
        var blobs =
            new List<BlobItemViewModel>();

        await foreach (
            var item
            in _blobContainer.GetBlobsAsync(
                cancellationToken:
                    cancellationToken))
        {
            var contentType =
                item.Properties.ContentType;

            if (string.IsNullOrWhiteSpace(
                contentType))
            {
                contentType =
                    GetContentType(item.Name);
            }

            blobs.Add(
                new BlobItemViewModel
                {
                    Name = item.Name,

                    ContentType =
                        contentType ??
                        "application/octet-stream",

                    Size =
                        item.Properties.ContentLength
                        ?? 0
                });
        }

        return blobs
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task UploadBlobAsync(
        string fileName,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name cannot be empty.",
                nameof(fileName));
        }

        var safeName =
            SanitizeFileName(fileName);

        var blob =
            _blobContainer.GetBlobClient(
                safeName);

        var resolvedContentType =
            string.IsNullOrWhiteSpace(contentType)
                ? GetContentType(safeName)
                : contentType;

        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders =
                    new BlobHttpHeaders
                    {
                        ContentType =
                            resolvedContentType
                    }
            },
            cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType)>
        DownloadBlobAsync(
            string name,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Blob name cannot be empty.",
                nameof(name));
        }

        var safeName =
            SanitizeFileName(name);

        var blob =
            _blobContainer.GetBlobClient(
                safeName);

        var response =
            await blob.DownloadStreamingAsync(
                options: null,
                cancellationToken:
                    cancellationToken);

        var contentType =
            response.Value.Details.ContentType;

        if (string.IsNullOrWhiteSpace(
            contentType))
        {
            contentType =
                GetContentType(safeName);
        }

        return
        (
            response.Value.Content,
            contentType
        );
    }

    public async Task SendQueueMessageAsync(
        string type,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException(
                "Message type cannot be empty.",
                nameof(type));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Message cannot be empty.",
                nameof(message));
        }

        var queueMessage =
            new
            {
                Type = type.Trim(),
                Message = message.Trim(),
                Created = DateTime.UtcNow
            };

        var json =
            JsonSerializer.Serialize(
                queueMessage);

        await _queue.SendMessageAsync(
            json,
            cancellationToken);
    }

    public async Task<
        (List<string> Messages, long ApproximateCount)>
        GetQueueDataAsync(
            CancellationToken cancellationToken = default)
    {
        var properties =
            await _queue.GetPropertiesAsync(
                cancellationToken);

        var messages =
            await _queue.PeekMessagesAsync(
                32,
                cancellationToken);

        return
        (
            messages.Value
                .Select(x => x.MessageText)
                .ToList(),

            properties.Value
                .ApproximateMessagesCount
        );
    }

    public async Task UploadLogAsync(
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name cannot be empty.",
                nameof(fileName));
        }

        var safeName =
            SanitizeFileName(fileName);

        var directory =
            _fileShareClient
                .GetRootDirectoryClient();

        var fileClient =
            directory.GetFileClient(
                safeName);

        var bytes =
            Encoding.UTF8.GetBytes(
                content ?? string.Empty);

        await fileClient.DeleteIfExistsAsync(
            cancellationToken:
                cancellationToken);

        await fileClient.CreateAsync(
            bytes.Length,
            options: null,
            conditions: null,
            cancellationToken:
                cancellationToken);

        await using var stream =
            new MemoryStream(bytes);

        await fileClient.UploadRangeAsync(
            new HttpRange(
                0,
                stream.Length),
            stream,
            options: null,
            cancellationToken:
                cancellationToken);
    }

    public async Task<List<LogFileViewModel>>
        GetLogFilesAsync(
            CancellationToken cancellationToken = default)
    {
        var files =
            new List<LogFileViewModel>();

        var directory =
            _fileShareClient
                .GetRootDirectoryClient();

        await foreach (
            var item
            in directory.GetFilesAndDirectoriesAsync(
                cancellationToken:
                    cancellationToken))
        {
            if (item.IsDirectory)
            {
                continue;
            }

            var fileClient =
                directory.GetFileClient(
                    item.Name);

            var properties =
                await fileClient.GetPropertiesAsync(
                    cancellationToken:
                        cancellationToken);

            files.Add(
                new LogFileViewModel
                {
                    Name =
                        item.Name,

                    Size =
                        properties.Value
                            .ContentLength,

                    LastModified =
                        properties.Value
                            .LastModified
                });
        }

        return files
            .OrderByDescending(
                x => x.LastModified)
            .ToList();
    }

    public async Task<Stream> DownloadLogAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name cannot be empty.",
                nameof(fileName));
        }

        var directory =
            _fileShareClient
                .GetRootDirectoryClient();

        var fileClient =
            directory.GetFileClient(
                SanitizeFileName(fileName));

        var response =
            await fileClient.DownloadAsync(
                options: null,
                cancellationToken:
                    cancellationToken);

        return response.Value.Content;
    }

    public async Task SeedDemoDataAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(
            cancellationToken);

        var customers =
            new[]
            {
                (
                    CustomerId: "CUST001",
                    FirstName: "Emma",
                    LastName: "Williams",
                    Email: "emma.williams@gmail.com",
                    Phone: "0712345689",
                    City: "Cape Town"
                ),
                (
                    CustomerId: "CUST002",
                    FirstName: "Liam",
                    LastName: "Johnson",
                    Email: "liam.johnson@gmail.com",
                    Phone: "0723456891",
                    City: "Pretoria"
                ),
                (
                    CustomerId: "CUST003",
                    FirstName: "Sophia",
                    LastName: "Brown",
                    Email: "sophia.brown@gmail.com",
                    Phone: "0731245689",
                    City: "Durban"
                ),
                (
                    CustomerId: "CUST004",
                    FirstName: "Daniel",
                    LastName: "Miller",
                    Email: "daniel.miller@gmail.com",
                    Phone: "0745689123",
                    City: "Kimberley"
                ),
                (
                    CustomerId: "CUST005",
                    FirstName: "Olivia",
                    LastName: "Davis",
                    Email: "olivia.davis@gmail.com",
                    Phone: "0756489123",
                    City: "Bloemfontein"
                )
            };

        foreach (var customer in customers)
        {
            await UpdateCustomerAsync(
                new CustomerEntity
                {
                    PartitionKey =
                        "Customers",

                    RowKey =
                        customer.CustomerId,

                    FirstName =
                        customer.FirstName,

                    LastName =
                        customer.LastName,

                    Email =
                        customer.Email,

                    Phone =
                        customer.Phone,

                    City =
                        customer.City,

                    CreatedAt =
                        DateTime.UtcNow
                },
                cancellationToken);
        }

        var products =
            new[]
            {
                (
                    ProductId: "PROD001",
                    ProductName: "Laptop",
                    Price: 12999.99,
                    StockQuantity: 10
                ),
                (
                    ProductId: "PROD002",
                    ProductName: "Mouse",
                    Price: 299.99,
                    StockQuantity: 25
                ),
                (
                    ProductId: "PROD003",
                    ProductName: "Keyboard",
                    Price: 149.99,
                    StockQuantity: 20
                ),
                (
                    ProductId: "PROD004",
                    ProductName: "Monitor",
                    Price: 3499.00,
                    StockQuantity: 15
                ),
                (
                    ProductId: "PROD005",
                    ProductName: "Headphones",
                    Price: 1000.00,
                    StockQuantity: 30
                )
            };

        foreach (var product in products)
        {
            await UpdateProductAsync(
                new ProductEntity
                {
                    PartitionKey =
                        "Products",

                    RowKey =
                        product.ProductId,

                    ProductName =
                        product.ProductName,

                    Price =
                        product.Price,

                    StockQuantity =
                        product.StockQuantity
                },
                cancellationToken);
        }

        for (var i = 1; i <= 5; i++)
        {
            var svg =
                $"""
                <svg xmlns="http://www.w3.org/2000/svg"
                     width="800"
                     height="450"
                     viewBox="0 0 800 450">

                    <rect
                        width="800"
                        height="450"
                        fill="#f4f4f5"/>

                    <text
                        x="400"
                        y="205"
                        text-anchor="middle"
                        font-family="Arial"
                        font-size="42"
                        fill="#18181b">

                        Product Media {i}

                    </text>

                    <text
                        x="400"
                        y="260"
                        text-anchor="middle"
                        font-family="Arial"
                        font-size="22"
                        fill="#52525b">

                        Azure Blob Storage

                    </text>

                </svg>
                """;

            await using var stream =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(svg));

            await UploadBlobAsync(
                $"product-{i}.svg",
                stream,
                "image/svg+xml",
                cancellationToken);
        }

        var queueData =
            await GetQueueDataAsync(
                cancellationToken);

        var currentCount =
            Math.Min(
                queueData.ApproximateCount,
                5);

        for (
            var i = (int)currentCount + 1;
            i <= 5;
            i++)
        {
            await SendQueueMessageAsync(
                i % 2 == 0
                    ? "Inventory"
                    : "Order",

                $"Demo transaction {i}: " +
                "processing order and inventory update.",

                cancellationToken);
        }

        for (var i = 1; i <= 5; i++)
        {
            await UploadLogAsync(
                $"application-log-{i}.log",

                $"Azure Storage Demo Log {i}" +
                Environment.NewLine +

                $"Timestamp: {DateTime.UtcNow:O}" +
                Environment.NewLine +

                "Status: Completed" +
                Environment.NewLine +

                "Operation: Demo storage transaction" +
                Environment.NewLine,

                cancellationToken);
        }
    }

    private static void ValidateTableKeys(
        string partitionKey,
        string rowKey)
    {
        if (string.IsNullOrWhiteSpace(
            partitionKey))
        {
            throw new ArgumentException(
                "Partition key cannot be empty.",
                nameof(partitionKey));
        }

        if (string.IsNullOrWhiteSpace(
            rowKey))
        {
            throw new ArgumentException(
                "Row key cannot be empty.",
                nameof(rowKey));
        }
    }

    public static string SanitizeFileName(
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(
            fileName))
        {
            return $"file-{Guid.NewGuid():N}";
        }

        var safeName =
            Path.GetFileName(fileName);

        foreach (
            var invalid
            in Path.GetInvalidFileNameChars())
        {
            safeName =
                safeName.Replace(
                    invalid,
                    '-');
        }

        return string.IsNullOrWhiteSpace(
            safeName)

            ? $"file-{Guid.NewGuid():N}"

            : safeName
                .Replace(" ", "-")
                .ToLowerInvariant();
    }

    public static string GetContentType(
        string fileName)
    {
        return Path
            .GetExtension(fileName)
            .ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" =>
                "image/jpeg",

            ".png" =>
                "image/png",

            ".gif" =>
                "image/gif",

            ".webp" =>
                "image/webp",

            ".svg" =>
                "image/svg+xml",

            ".mp4" =>
                "video/mp4",

            ".webm" =>
                "video/webm",

            ".mov" =>
                "video/quicktime",

            ".mp3" =>
                "audio/mpeg",

            ".wav" =>
                "audio/wav",

            ".m4a" =>
                "audio/mp4",

            ".txt" or ".log" =>
                "text/plain",

            _ =>
                "application/octet-stream"
        };
    }
}