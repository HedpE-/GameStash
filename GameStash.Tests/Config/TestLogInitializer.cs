using Serilog;

namespace GameStash.Tests.Config
{
    public static class TestLogInitializer
    {
        private static bool _isInitialized = false;

        /**
         * Bypasses memory buffering and links Serilog directly to NUnit's 
         * immediate output context stream for live terminal tracing.
         */
        public static void InitializeTestLogger()
        {
            if (_isInitialized) return;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.NUnitOutput(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.ForContext(typeof(TestLogInitializer)).Information("Test logging infrastructure successfully bound to NUnit execution context.");
            _isInitialized = true;
        }
    }
}
