namespace NamClient.Web.Models;

/// <summary>Search index: one word of a client's name, matched with LIKE 'word%' instead of scanning every name.</summary>
public class ClientNameWord
{
    public const int MaxLength = 100;

    public string Word { get; set; } = string.Empty;

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;
}
