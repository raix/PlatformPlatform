namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Callback for customizing Scaleway resource configuration before provisioning.
/// </summary>
/// <typeparam name="T">The configuration type being customized.</typeparam>
public delegate void ScalewayPublishCallback<in T>(T config);
