using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.Models;

namespace NamClient.Web.Services.Search;

/// <summary>Every term must match the start of the name, surname or email.</summary>
public static class ContactSearch
{
    public static IQueryable<Contact> Apply(IQueryable<Contact> contacts, string? search)
    {
        var terms = SearchText.Terms(search);
        if (terms.Count > 0)
        {
            contacts = contacts.TagWith(SearchQueryHintInterceptor.Tag);
        }

        foreach (var term in terms)
        {
            contacts = contacts.Where(c =>
                c.Name.StartsWith(term) ||
                c.Surname.StartsWith(term) ||
                c.Email.StartsWith(term));
        }

        return contacts;
    }
}
