using Microsoft.AspNetCore.Mvc;
using NamClient.Web.Data.Paging;
using NamClient.Web.Infrastructure;
using NamClient.Web.Services;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Controllers;

public class ContactsController : Controller
{
    private const string FormView = "Form";
    private const string TableView = "_ContactsTable";
    private const string ClientsPanelView = "_ClientsPanel";
    private const string ClientOptionsView = "_LinkOptions";
    private const string DuplicateEmailMessage = "This email address is already used by another contact.";

    private readonly IContactService _contacts;
    private readonly IClientContactLinkService _links;

    public ContactsController(IContactService contacts, IClientContactLinkService links)
    {
        _contacts = contacts;
        _links = links;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var page = await _contacts.SearchAsync(request, cancellationToken);

        // The page emptied (e.g. its rows were deleted): show the first page.
        if (page.Items.Count == 0 && (request.After is not null || request.Before is not null))
        {
            request.After = request.Before = null;
            page = await _contacts.SearchAsync(request, cancellationToken);
        }

        var model = new ListPageViewModel<ContactListItemViewModel>
        {
            Request = request,
            Sort = _contacts.NormaliseSort(request.Sort),
            Page = page,
        };

        return Request.IsAjax() ? PartialView(TableView, model) : View(model);
    }

    [HttpGet]
    public async Task Export(string? q, string? sort, string? ids, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"contacts-{DateTime.Now:yyyyMMdd-HHmm}.csv\"";

        await using var writer = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await CsvWriter.WriteRowAsync(writer, "Name", "Surname", "Email address", "No. of linked clients");

        await foreach (var contact in _contacts.ExportAsync(q, sort, HttpRequestExtensions.ParseIds(ids)).WithCancellation(cancellationToken))
        {
            await CsvWriter.WriteRowAsync(writer, contact.Name, contact.Surname, contact.Email, contact.LinkedClientCount.ToString());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return Request.IsAjax()
                ? BadRequest(new { message = "Select at least one contact to delete." })
                : RedirectToAction(nameof(Index));
        }

        var deleted = await _contacts.DeleteAsync(ids, cancellationToken);
        var message = deleted == 1 ? "1 contact deleted." : $"{deleted:N0} contacts deleted.";

        if (Request.IsAjax())
        {
            return Json(new { message });
        }

        TempData.SetStatus(StatusMessage.Success(message));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create() => View(FormView, new ContactFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContactFormViewModel model, CancellationToken cancellationToken)
    {
        model.ContactId = null;

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        var contact = await _contacts.CreateAsync(model.Name, model.Surname, model.Email, cancellationToken);
        if (contact is null)
        {
            ModelState.AddModelError(nameof(model.Email), DuplicateEmailMessage);
            return View(FormView, model);
        }

        TempData.SetStatus(StatusMessage.Success($"Contact \"{contact.FullName}\" saved."));
        return RedirectToAction(nameof(Edit), new { id = contact.ContactId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var contact = await _contacts.FindAsync(id, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        return View(FormView, new ContactFormViewModel
        {
            ContactId = contact.ContactId,
            Name = contact.Name,
            Surname = contact.Surname,
            Email = contact.Email,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContactFormViewModel model, CancellationToken cancellationToken)
    {
        var contact = await _contacts.FindAsync(id, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        model.ContactId = id;
        model.CreatedAt = contact.CreatedAt;
        model.UpdatedAt = contact.UpdatedAt;

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        switch (await _contacts.UpdateAsync(id, model.Name, model.Surname, model.Email, cancellationToken))
        {
            case SaveResult.NotFound:
                return NotFound();

            case SaveResult.DuplicateEmail:
                ModelState.AddModelError(nameof(model.Email), DuplicateEmailMessage);
                return View(FormView, model);
        }

        TempData.SetStatus(StatusMessage.Success("Contact saved."));
        return RedirectToAction(nameof(Edit), new { id });
    }

    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> VerifyEmail(string email, int? contactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Json(true);
        }

        return await _contacts.EmailExistsAsync(email, contactId, cancellationToken)
            ? Json(DuplicateEmailMessage)
            : Json(true);
    }

    [HttpGet]
    public async Task<IActionResult> Clients(int id, CancellationToken cancellationToken)
    {
        if (await _contacts.FindAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        return await ClientsPanel(id, status: null, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> ClientOptions(int id, string? q, CancellationToken cancellationToken) =>
        PartialView(ClientOptionsView, await _contacts.SearchLinkableClientsAsync(id, q, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkClients(int id, [FromForm] int[] clientIds, CancellationToken cancellationToken)
    {
        if (clientIds.Length == 0)
        {
            return await ClientsPanel(id, StatusMessage.Error("Please select at least one client to link."), cancellationToken);
        }

        var outcome = await _links.LinkClientsToContactAsync(id, clientIds, cancellationToken);
        if (!outcome.OwnerFound)
        {
            return NotFound();
        }

        return await ClientsPanel(id, LinkMessages.Describe(outcome, "client", "clients"), cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkClients(int id, [FromForm] int[] clientIds, CancellationToken cancellationToken)
    {
        if (clientIds.Length == 0)
        {
            return await ClientsPanel(id, StatusMessage.Error("Please select at least one client to unlink."), cancellationToken);
        }

        var removed = await _links.UnlinkClientsFromContactAsync(id, clientIds, cancellationToken);
        return await ClientsPanel(id, LinkMessages.DescribeUnlink(removed, "client", "clients"), cancellationToken);
    }

    private async Task<IActionResult> ClientsPanel(int contactId, StatusMessage? status, CancellationToken cancellationToken) =>
        PartialView(ClientsPanelView, new ContactClientsPanelViewModel
        {
            ContactId = contactId,
            Clients = await _contacts.GetLinkedClientsAsync(contactId, cancellationToken),
            Status = status,
        });
}
