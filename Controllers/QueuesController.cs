using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;

using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class QueuesController : Controller
{
    private readonly AzureStorageService _storage;

    public QueuesController(
        AzureStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        await _storage.EnsureResourcesAsync(
            cancellationToken);

        var queue =
            await _storage.GetQueueDataAsync(
                cancellationToken);

        return View(
            new QueuePageViewModel
            {
                Messages =
                    queue.Messages,

                ApproximateMessageCount =
                    queue.ApproximateCount
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        QueueMessageViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            string.IsNullOrWhiteSpace(model.Message))
        {
            TempData["Error"] =
                "Enter a valid queue message.";

            return RedirectToAction(
                nameof(Index));
        }

        var type =
            string.Equals(
                model.Type,
                "Inventory",
                StringComparison.OrdinalIgnoreCase)
                ? "Inventory"
                : "Order";

        await _storage.SendQueueMessageAsync(
            type,
            model.Message.Trim(),
            cancellationToken);

        TempData["Success"] =
            "Transaction message added to Azure Queue Storage.";

        return RedirectToAction(
            nameof(Index));
    }
}