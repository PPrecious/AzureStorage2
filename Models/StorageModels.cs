using Azure;
using Azure.Data.Tables;

namespace AzureStorageWebApp.Models;

public class CustomerEntity : ITableEntity
{
    public string PartitionKey { get; set; } =
        "Customers";

    public string RowKey { get; set; } =
        Guid.NewGuid().ToString("N");

    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string Phone { get; set; } =
        string.Empty;

    public string City { get; set; } =
        string.Empty;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}

public class CustomerProfile : ITableEntity
{
    public string PartitionKey { get; set; } =
        "Customers";

    public string RowKey { get; set; } =
        Guid.NewGuid().ToString("N");

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string Phone { get; set; } =
        string.Empty;
}

public class ProductEntity : ITableEntity
{
    public string PartitionKey { get; set; } =
        "Products";

    public string RowKey { get; set; } =
        Guid.NewGuid().ToString("N");

    public string ProductName { get; set; } =
        string.Empty;

    public double Price { get; set; }

    public int StockQuantity { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}

public class Product : ITableEntity
{
    public string PartitionKey { get; set; } =
        "Products";

    public string RowKey { get; set; } =
        Guid.NewGuid().ToString("N");

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public double Price { get; set; }

    public int Stock { get; set; }
}

public class StorageDashboardViewModel
{
    public int CustomerCount { get; set; }

    public int ProductCount { get; set; }

    public int BlobCount { get; set; }

    public long QueueCount { get; set; }

    public int FileCount { get; set; }

    public string? Message { get; set; }
}

public class QueueMessageViewModel
{
    public string Type { get; set; } =
        "Order";

    public string Message { get; set; } =
        string.Empty;
}

public class QueuePageViewModel
{
    public List<string> Messages { get; set; } =
        new();

    public long ApproximateMessageCount { get; set; }
}

public class BlobItemViewModel
{
    public string Name { get; set; } =
        string.Empty;

    public string ContentType { get; set; } =
        string.Empty;

    public long Size { get; set; }

    public bool IsImage =>
        ContentType.StartsWith(
            "image/",
            StringComparison.OrdinalIgnoreCase);

    public bool IsVideo =>
        ContentType.StartsWith(
            "video/",
            StringComparison.OrdinalIgnoreCase);

    public bool IsAudio =>
        ContentType.StartsWith(
            "audio/",
            StringComparison.OrdinalIgnoreCase);
}

public class LogFileViewModel
{
    public string Name { get; set; } =
        string.Empty;

    public long Size { get; set; }

    public DateTimeOffset? LastModified { get; set; }
}
