using Microsoft.Playwright;

namespace GameStash.Tests.Playwright.Setup
{
    [TestFixture]
    public abstract class ApiTestBase
    {
        private IPlaywright? _playwright;
        protected IAPIRequestContext ApiRequest { get; private set; } = null!;

        // Base URL pointing to the running GameStash server instance
        protected virtual string BaseUrl => Environment.GetEnvironmentVariable("GAMESTASH_TEST_BASE_URL") ?? "http://localhost:5088";

        [OneTimeSetUp]
        public async Task GlobalApiSetupAsync()
        {
            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        }

        [SetUp]
        public async Task SetupApiContextAsync()
        {
            // Configure API context with default JSON headers and target Base URL
            ApiRequest = await _playwright!.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                BaseURL = BaseUrl,
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    { "Accept", "application/json" }
                },
                IgnoreHTTPSErrors = true
            });
        }

        [TearDown]
        public async Task TearDownApiContextAsync()
        {
            if (ApiRequest != null)
            {
                await ApiRequest.DisposeAsync();
            }
        }

        [OneTimeTearDown]
        public void GlobalApiTearDown()
        {
            _playwright?.Dispose();
        }
    }
}