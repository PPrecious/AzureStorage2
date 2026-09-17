using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class HomeController : Controller
{
    private readonly AzureStorageService _storage;


    public HomeController(
        AzureStorageService storage)
    {
        _storage = storage;
    }


    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);


            var queue =
                await _storage.GetQueueDataAsync(
                    cancellationToken);


            var model =
                new StorageDashboardViewModel
                {
                    CustomerCount =
                        (await _storage
                            .GetCustomersAsync(
                                cancellationToken))
                        .Count,

                    ProductCount =
                        (await _storage
                            .GetProductsAsync(
                                cancellationToken))
                        .Count,

                    BlobCount =
                        (await _storage
                            .GetBlobsAsync(
                                cancellationToken))
                        .Count,

                    QueueCount =
                        queue.ApproximateCount,

                    FileCount =
                        (await _storage
                            .GetLogFilesAsync(
                                cancellationToken))
                        .Count
                };


            return View(model);
        }
        catch (Exception ex)
        {
            return View(
                new StorageDashboardViewModel
                {
                    Message =
                        ex.Message
                });
        }
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seed(
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.SeedDemoDataAsync(
                cancellationToken);


            TempData["Success"] =
                "Demo data created successfully. " +
                "Each Azure Storage service now contains " +
                "at least five assessment records/messages/files.";
        }
        catch (Exception ex)
        {
            TempData["Error"] =
                ex.Message;
        }


        return RedirectToAction(
            nameof(Index));
    }
}