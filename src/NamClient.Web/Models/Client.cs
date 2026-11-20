namespace NamClient.Web.Models;

public class Client : IAuditable
{
    public int ClientId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ClientCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ClientContact> ClientContacts { get; set; } = new List<ClientContact>();

    public ICollection<ClientNameWord> NameWords { get; set; } = new List<ClientNameWord>();
}
