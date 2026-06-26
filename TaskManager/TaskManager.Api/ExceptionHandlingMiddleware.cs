using Microsoft.Extensions.Options;

namespace TaskManager.Api
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext
            , IOptions<TaskManagerOptions> options)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción no controlada: {ex.Message}");

                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "application/json";

                await httpContext.Response.WriteAsJsonAsync(new
                {
                    Error = options.Value.GeneralErrorApp,
                    // En producción NUNCA expongas ex.Message ni el stack trace
                });
                   
                throw;
            }
        }
    }
}
