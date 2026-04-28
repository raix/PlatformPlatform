namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Transactional Email (TEM) domain.
/// Provides SMTP credentials for sending transactional emails.
/// </summary>
public sealed class ScalewayTemDomainResource(string name)
    : Resource(name), IScalewayResource, IResourceWithConnectionString
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string DomainName { get; set; } = string.Empty;

    public bool AcceptTos { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public string SmtpHost => $"smtp.tem.scw.cloud";

    public int SmtpPort { get; set; } = 465;

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"smtp://{SmtpHost}:{SmtpPort.ToString()}");
}
