using NamClient.Web.Data.Paging;

namespace NamClient.Web.ViewModels;

public sealed record LinkOptionViewModel(int Id, string Title, string Subtitle);

public sealed record LinkOptionsViewModel(IReadOnlyList<LinkOptionViewModel> Options, bool HasMore, string? Search);

public sealed record LinkPickerViewModel(
    string Title,
    string Noun,
    string NounPlural,
    string OptionsUrl,
    string LinkUrl,
    string FieldName,
    string SearchPlaceholder);

public sealed record StatusMessage(string Text, bool IsError = false)
{
    public static StatusMessage Success(string text) => new(text);

    public static StatusMessage Error(string text) => new(text, IsError: true);
}

public sealed record SortHeaderViewModel(string Label, string Key, string CurrentSort, string Url, string? CssClass = null)
{
    public bool IsActive => CurrentSort.TrimStart('-') == Key;

    public bool IsDescending => IsActive && CurrentSort.StartsWith('-');

    public string NextSort => IsActive && !IsDescending ? "-" + Key : Key;

    public string AriaSort => !IsActive ? "none" : IsDescending ? "descending" : "ascending";
}

public sealed record PagerViewModel(int Shown, int Total, bool TotalIsLowerBound, string NounPlural, string? PreviousUrl, string? NextUrl)
{
    public string Summary =>
        Total == 0
            ? string.Empty
            : $"Showing {Shown:N0} of {Total:N0}{(TotalIsLowerBound ? "+" : string.Empty)} {NounPlural}";
}

public sealed record ListToolbarViewModel(string ListUrl, string NounPlural, string SearchPlaceholder, string? Search, int PageSize);

public sealed class ListPageViewModel<T>
{
    public required PageRequest Request { get; init; }

    public required PagedResult<T> Page { get; init; }

    public required string Sort { get; init; }
}
