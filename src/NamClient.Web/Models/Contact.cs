namespace NamClient.Web.Models;

public class Contact : IAuditable
{
    public int ContactId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string FullName => $"{Surname} {Name}";

    public ICollection<ClientContact> ClientContacts { get; set; } = new List<ClientContact>();
}
