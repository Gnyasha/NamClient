using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Controllers;

public static class TempDataExtensions
{
    private const string Key = "StatusMessage";

    public static void SetStatus(this ITempDataDictionary tempData, StatusMessage message) =>
        tempData[Key] = JsonSerializer.Serialize(message);

    public static StatusMessage? GetStatus(this ITempDataDictionary tempData) =>
        tempData[Key] is string json ? JsonSerializer.Deserialize<StatusMessage>(json) : null;
}
