using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NamClient.Web.Data;

namespace NamClient.Web.ViewModels;

public sealed record ContactListItemViewModel
{
    public int ContactId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Surname { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int LinkedClientCount { get; init; }
}

public sealed record LinkedClientViewModel(int ClientId, string Name, string ClientCode);

public class ContactFormViewModel
{
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public int? ContactId { get; set; }

    [Required(ErrorMessage = "Please enter the contact's name.")]
    [StringLength(AppDbContext.NameMaxLength, ErrorMessage = "Name may not be longer than {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the contact's surname.")]
    [StringLength(AppDbContext.NameMaxLength, ErrorMessage = "Surname may not be longer than {1} characters.")]
    [Display(Name = "Surname")]
    public string Surname { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter an email address.")]
    [StringLength(AppDbContext.EmailMaxLength, ErrorMessage = "Email may not be longer than {1} characters.")]
    [RegularExpression(EmailPattern, ErrorMessage = "Please enter a valid email address, e.g. name@example.com.")]
    [Remote(action: "VerifyEmail", controller: "Contacts", AdditionalFields = nameof(ContactId))]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsNew => ContactId is null;
}

public sealed class ContactClientsPanelViewModel
{
    public int ContactId { get; init; }

    public IReadOnlyList<LinkedClientViewModel> Clients { get; init; } = [];

    public StatusMessage? Status { get; init; }
}
