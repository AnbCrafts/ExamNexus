using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure.Data;
using ExamNexus.Web.Models;

namespace ExamNexus.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext dbContext, ILogger<HomeController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.ActiveTenantsCount = await _dbContext.Tenants.CountAsync(t => t.IsActive);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPlan(SubscriptionRequestInputDto model)
    {
        if (!ModelState.IsValid)
        {
            TempData["PlanRequestError"] = "Please provide all required fields correctly to submit a plan inquiry.";
            return RedirectToAction(nameof(Index));
        }

        var request = new SubscriptionRequest
        {
            Id = Guid.NewGuid(),
            OrganizationName = model.OrganizationName.Trim(),
            SuggestedIdentifier = model.SuggestedIdentifier?.Trim().ToLowerInvariant(),
            ContactName = model.ContactName.Trim(),
            ContactEmail = model.ContactEmail.Trim().ToLowerInvariant(),
            ContactPhone = model.ContactPhone?.Trim(),
            RequestedPlan = model.RequestedPlan,
            EstimatedCandidates = model.EstimatedCandidates > 0 ? model.EstimatedCandidates : 500,
            AdditionalNotes = model.AdditionalNotes?.Trim(),
            Status = RequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.SubscriptionRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("New subscription plan request received from {Org} ({Email}) for {Plan}",
            request.OrganizationName, request.ContactEmail, request.RequestedPlan);

        TempData["PlanRequestSuccess"] = $"Thank you, {model.ContactName}! Your request for {model.OrganizationName} ({model.RequestedPlan} Plan) has been submitted to platform administrators for fast-track provisioning.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult QuickJoin(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction("Join", "Exam", new { code = code.Trim().ToUpperInvariant() });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
