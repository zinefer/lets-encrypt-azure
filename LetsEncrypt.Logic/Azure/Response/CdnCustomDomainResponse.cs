namespace LetsEncrypt.Logic.Azure.Response
{
    public class CdnCustomDomainResponse
    {
        public string HostName { get; set; }

        public CustomHttpsProvisioningState CustomHttpsProvisioningState { get; set; }

        public CustomHttpsProvisioningSubstate CustomHttpsProvisioningSubstate { get; set; }

        public CustomHttpsParameters CustomHttpsParameters { get; set; }
    }

    public class CustomHttpsParameters
    {
        public string CertificateSource { get; set; }

        public string ProtocolType { get; set; }

        public CertificateSourceParameters CertificateSourceParameters { get; set; }
    }

    public class CertificateSourceParameters
    {
        public string VaultName { get; set; }

        public string SecretName { get; set; }

        public string SecretVersion { get; set; }
    }

    public enum CustomHttpsProvisioningState
    {
        Unknown = 0,
        Enabling,
        Enabled,
        Disabling,
        Disabled,
        Failed
    }

    public enum CustomHttpsProvisioningSubstate
    {
        None = 0,
        SubmittingDomainControlValidationRequest,
        PendingDomainControlValidationRequestApproval,
        DomainControlValidationRequestApproved,
        DomainControlValidationRequestRejected,
        DomainControlValidationRequestTimedOut,
        IssuingCertificate,
        DeployingCertificate,
        CertificateDeployed,
        DeletingCertificate,
        CertificateDeleted
    }
}
