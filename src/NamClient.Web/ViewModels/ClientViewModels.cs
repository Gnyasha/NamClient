using System.ComponentModel.DataAnnotations;
using NamClient.Web.Data;

namespace NamClient.Web.ViewModels;

public sealed record ClientListItemViewModel
{
    public int ClientId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ClientCode { get; init; } = string.Empty;

    public int LinkedContactCount { get; init; }
}

public sealed record LinkedContactViewModel(int ContactId, string FullName, string Email);

public class ClientFormViewModel
{
    public int? ClientId { get; set; }

    [Required(ErrorMessage = "Please enter the client's name.")]
    [StringLength(AppDbContext.NameMaxLength, ErrorMessage = "Name may not be longer than {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Client code")]
    public string? ClientCode { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsNew => ClientId is null;
}

public sealed class ClientContactsPanelViewModel
{
    public int ClientId { get; init; }

    public IReadOnlyList<LinkedContactViewModel> Contacts { get; init; } = [];

    public StatusMessage? Status { get; init; }
}
