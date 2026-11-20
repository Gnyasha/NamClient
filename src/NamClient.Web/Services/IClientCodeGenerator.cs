namespace NamClient.Web.Services;

public interface IClientCodeGenerator
{
    Task<string> GenerateAsync(string clientName, CancellationToken cancellationToken = default);
}
