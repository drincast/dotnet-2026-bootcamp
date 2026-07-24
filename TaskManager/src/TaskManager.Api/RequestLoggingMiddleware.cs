namespace TaskManager.Api
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var start = DateTime.UtcNow;

            Console.WriteLine($"→ (Begin) - {context.Request.Method} {context.Request.Path}");

            await _next(context); // pasa al siguiente

            Console.WriteLine($"----");
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            Console.WriteLine($"← (End) {context.Response.StatusCode} ({elapsed:F0}ms)");
        }
    }
}
