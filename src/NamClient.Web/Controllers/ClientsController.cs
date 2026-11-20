using Microsoft.AspNetCore.Mvc;
using NamClient.Web.Data.Paging;
using NamClient.Web.Infrastructure;
using NamClient.Web.Services;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Controllers;

public class ClientsController : Controller
{
    private const string FormView = "Form";
    private const string TableView = "_ClientsTable";
    private const string ContactsPanelView = "_ContactsPanel";
    private const string ContactOptionsView = "_LinkOptions";

    private readonly IClientService _clients;
    private readonly IClientContactLinkService _links;

    public ClientsController(IClientService clients, IClientContactLinkService links)
    {
        _clients = clients;
        _links = links;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var page = await _clients.SearchAsync(request, cancellationToken);

        // The page emptied (e.g. its rows were deleted): show the first page.
        if (page.Items.Count == 0 && (request.After is not null || request.Before is not null))
        {
            request.After = request.Before = null;
            page = await _clients.SearchAsync(request, cancellationToken);
        }

        var model = new ListPageViewModel<ClientListItemViewModel>
        {
            Request = request,
            Sort = _clients.NormaliseSort(request.Sort),
            Page = page,
        };

        return Request.IsAjax() ? PartialView(TableView, model) : View(model);
    }

    [HttpGet]
    public async Task Export(string? q, string? sort, string? ids, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"clients-{DateTime.Now:yyyyMMdd-HHmm}.csv\"";

        await using var writer = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await CsvWriter.WriteRowAsync(writer, "Name", "Client code", "No. of linked contacts");

        await foreach (var client in _clients.ExportAsync(q, sort, HttpRequestExtensions.ParseIds(ids)).WithCancellation(cancellationToken))
        {
            await CsvWriter.WriteRowAsync(writer, client.Name, client.ClientCode, client.LinkedContactCount.ToString());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return Request.IsAjax()
                ? BadRequest(new { message = "Select at least one client to delete." })
                : RedirectToAction(nameof(Index));
        }

        var deleted = await _clients.DeleteAsync(ids, cancellationToken);
        var message = deleted == 1 ? "1 client deleted." : $"{deleted:N0} clients deleted.";

        if (Request.IsAjax())
        {
            return Json(new { message });
        }

        TempData.SetStatus(StatusMessage.Success(message));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create() => View(FormView, new ClientFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClientFormViewModel model, CancellationToken cancellationToken)
    {
        model.ClientId = null;
        model.ClientCode = null;

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        var client = await _clients.CreateAsync(model.Name, cancellationToken);

        TempData.SetStatus(StatusMessage.Success($"Client \"{client.Name}\" saved with client code {client.ClientCode}."));
        return RedirectToAction(nameof(Edit), new { id = client.ClientId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var client = await _clients.FindAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        return View(FormView, new ClientFormViewModel
        {
            ClientId = client.ClientId,
            Name = client.Name,
            ClientCode = client.ClientCode,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClientFormViewModel model, CancellationToken cancellationToken)
    {
        var client = await _clients.FindAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        model.ClientId = id;
        model.ClientCode = client.ClientCode;
        model.CreatedAt = client.CreatedAt;
        model.UpdatedAt = client.UpdatedAt;

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        await _clients.UpdateAsync(id, model.Name, cancellationToken);

        TempData.SetStatus(StatusMessage.Success("Client saved."));
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Contacts(int id, CancellationToken cancellationToken)
    {
        if (await _clients.FindAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        return await ContactsPanel(id, status: null, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> ContactOptions(int id, string? q, CancellationToken cancellationToken) =>
        PartialView(ContactOptionsView, await _clients.SearchLinkableContactsAsync(id, q, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkContacts(int id, [FromForm] int[] contactIds, CancellationToken cancellationToken)
    {
        if (contactIds.Length == 0)
        {
            return await ContactsPanel(id, StatusMessage.Error("Please select at least one contact to link."), cancellationToken);
        }

        var outcome = await _links.LinkContactsToClientAsync(id, contactIds, cancellationToken);
        if (!outcome.OwnerFound)
        {
            return NotFound();
        }

        return await ContactsPanel(id, LinkMessages.Describe(outcome, "contact", "contacts"), cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkContacts(int id, [FromForm] int[] contactIds, CancellationToken cancellationToken)
    {
        if (contactIds.Length == 0)
        {
            return await ContactsPanel(id, StatusMessage.Error("Please select at least one contact to unlink."), cancellationToken);
        }

        var removed = await _links.UnlinkContactsFromClientAsync(id, contactIds, cancellationToken);
        return await ContactsPanel(id, LinkMessages.DescribeUnlink(removed, "contact", "contacts"), cancellationToken);
    }

    private async Task<IActionResult> ContactsPanel(int clientId, StatusMessage? status, CancellationToken cancellationToken) =>
        PartialView(ContactsPanelView, new ClientContactsPanelViewModel
        {
            ClientId = clientId,
            Contacts = await _clients.GetLinkedContactsAsync(clientId, cancellationToken),
            Status = status,
        });
}
