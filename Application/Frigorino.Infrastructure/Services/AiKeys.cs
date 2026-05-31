namespace Frigorino.Infrastructure.Services
{
    // Keys for the two keyed ChatClient registrations. Both share one API key but use
    // per-feature models, so they must be distinguished in DI.
    public static class AiKeys
    {
        public const string Classifier = "ai-classifier";
        public const string Extractor = "ai-extractor";
    }
}
