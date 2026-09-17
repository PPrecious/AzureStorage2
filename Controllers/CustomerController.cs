using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;

using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class CustomersController : Controller
{
    private readonly AzureStorageService _storage;

    public CustomersController(
        AzureStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        await _storage.EnsureResourcesAsync(
            cancellationToken);

        return View(
            await _storage.GetCustomersAsync(
                cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CustomerEntity model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);

            return View(
                "Index",
                await _storage.GetCustomersAsync(
                    cancellationToken));
        }

        model.PartitionKey = "Customers";

        model.RowKey =
            string.IsNullOrWhiteSpace(model.RowKey)
                ? Guid.NewGuid().ToString("N")
                : model.RowKey;

        model.CreatedAt = DateTime.UtcNow;

        await _storage.AddCustomerAsync(
            model,
            cancellationToken);

        TempData["Success"] =
            "Customer added successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        CustomerEntity model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);

            return View(
                "Index",
                await _storage.GetCustomersAsync(
                    cancellationToken));
        }

        model.PartitionKey = "Customers";

        await _storage.UpdateCustomerAsync(
            model,
            cancellationToken);

        TempData["Success"] =
            "Customer updated successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string rowKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rowKey))
        {
            TempData["Error"] =
                "Customer could not be deleted.";

            return RedirectToAction(
                nameof(Index));
        }

        await _storage.DeleteCustomerAsync(
            "Customers",
            rowKey,
            cancellationToken);

        TempData["Success"] =
            "Customer deleted successfully.";

        return RedirectToAction(
            nameof(Index));
    }
}
