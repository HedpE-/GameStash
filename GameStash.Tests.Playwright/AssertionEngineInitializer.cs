using FluentAssertions;
using FluentAssertions.Extensibility;
using GameStash.Tests.Playwright;

// This attribute executes BEFORE tests or engines are initialized
[assembly: AssertionEngineInitializer(
    typeof(AssertionEngineInitializer),
    nameof(AssertionEngineInitializer.AcknowledgeSoftWarning))]

namespace GameStash.Tests.Playwright
{
    public static class AssertionEngineInitializer
    {
        public static void AcknowledgeSoftWarning()
        {
            License.Accepted = true;
        }
    }
}