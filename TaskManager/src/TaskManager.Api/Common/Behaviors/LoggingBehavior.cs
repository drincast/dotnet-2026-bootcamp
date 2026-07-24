using MediatR;
using System.Diagnostics;

namespace TaskManager.Api.Common.Behaviors
{
    /// <summary>
    /// Pipeline Behavior que loguea cada Command/Query que pasa por MediatR.
    /// Registra: nombre del request y tiempo total de ejecución.
    /// Se ejecuta ANTES del Handler (envuelve la cadena).
    /// </summary>
    /// <remarks>
    /// Este behavior complementa el RequestLoggingMiddleware de Semana 1:
    /// - El middleware ve: método HTTP, path, status code
    /// - El behavior ve: nombre del Command/Query, datos del request, tiempo de Handler
    /// Juntos dan la visión completa del flujo de una petición.
    /// </remarks>
    public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        // Umbral en milisegundos para marcar como warning
        private const int SlowRequestThresholdMs = 500;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                 "[LoggingBehavior] Handling {RequestName}",
                requestName);

            // 2. Ejecuta el siguiente paso de la cadena (otro behavior o el Handler)
            TResponse response;

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[LoggingBehavior] Request data: {@Request}", request);

                response = await next(ct);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // 3. Si explota, loguea el error y re-lanza
                // NO tragarse la excepción — debe seguir hacia arriba
                // para que ExceptionHandlingMiddleware la capture
                _logger.LogError(
                    ex,
                    "[LoggingBehavior] Request {RequestName} threw exception en {ElapsedMs}ms",
                    requestName, stopwatch.ElapsedMilliseconds);
                throw;
            }

            // 4. DESPUÉS del Handler — mide y loguea el tiempo
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                // Loguea como warning si pasó el umbral
                _logger.LogWarning(
                    "[LoggingBehavior] Request {RequestName} completed in {ElapsedMs}ms (SLOW)",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // Loguea como información normal
                _logger.LogInformation(
                    "[LoggingBehavior] Handled {RequestName} in {ElapsedMs}ms",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
    }
}
