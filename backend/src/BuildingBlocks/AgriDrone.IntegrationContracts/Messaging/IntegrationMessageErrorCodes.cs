namespace AgriDrone.IntegrationContracts.Messaging;

public static class IntegrationMessageErrorCodes
{
    public const string BodyEmpty = "IntegrationMessage.BodyEmpty";

    public const string BodyTooLarge = "IntegrationMessage.BodyTooLarge";

    public const string MalformedJson = "IntegrationMessage.MalformedJson";

    public const string EnvelopeInvalid = "IntegrationMessage.EnvelopeInvalid";

    public const string ActorRequired = "IntegrationMessage.ActorRequired";

    public const string PayloadInvalid = "IntegrationMessage.PayloadInvalid";
}
