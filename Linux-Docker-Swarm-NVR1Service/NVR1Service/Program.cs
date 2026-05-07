using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IRabbitMQConsumer, RabbitMQConsumer>();
builder.Services.AddHostedService<NVR1Service>();
builder.Services.AddLogging();

var app = builder.Build();

app.MapGet("/", () => "NVR1Service is running");

app.Run();

// Models
public class CameraEvent
{
    public string CameraId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}

// Interfaces
public interface IRabbitMQConsumer
{
    Task StartConsumingAsync(Func<CameraEvent, Task> eventHandler, CancellationToken cancellationToken);
}

// RabbitMQ Consumer
public class RabbitMQConsumer : IRabbitMQConsumer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private const string ExchangeName = "camera_events";
    private const string QueueName = "nvr1_queue";
    private const string RoutingKey = "CAMERA-001"; // must match exactly

    public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger)
    {
        _logger = logger;
        
    var factory = new ConnectionFactory()
    {
        HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbit",
        Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672"),
        UserName = "guest",
        Password = "guest",
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        DispatchConsumersAsync = true
    };

        try
        {
            var maxAttempts = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_RETRIES"), out var parsedRetries)
                ? parsedRetries
                : 20;
            var retryDelayMs = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_RETRY_DELAY_MS"), out var parsedDelay)
                ? parsedDelay
                : 3000;

            _connection = CreateConnectionWithRetry(factory, maxAttempts, retryDelayMs);
            _channel = _connection.CreateModel();
            
            // Declare exchange
            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Headers, durable: true);
            
            // Declare queue
            _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
            
            // bind queue with header‐matching criteria:
            // here we want messages where interestedParty = "CAMERA-001"
            var bindArgs = new Dictionary<string, object>
            {
                ["x-match"]         = "all",       // or "any"
                ["interestedParty"] = "CAMERA-001"
            };

            // Bind queue to exchange with routing key
            _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: string.Empty, arguments: bindArgs);
            
            // Check queue message count
            var queueInfo = _channel.QueueDeclarePassive(QueueName);
            _logger.LogInformation($"RabbitMQ Consumer initialized. Queue '{QueueName}' bound to exchange '{ExchangeName}' with routing key '{RoutingKey}'. Messages in queue: {queueInfo.MessageCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Consumer");
            throw;
        }
    }

    private IConnection CreateConnectionWithRetry(ConnectionFactory factory, int maxAttempts, int retryDelayMs)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return factory.CreateConnection();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxAttempts)
                {
                    break;
                }

                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed. Retrying in {RetryDelayMs} ms.",
                    attempt,
                    maxAttempts,
                    retryDelayMs);

                Thread.Sleep(retryDelayMs);
            }
        }

        throw new InvalidOperationException("Failed to connect to RabbitMQ after multiple attempts.", lastException);
    }

    public async Task StartConsumingAsync(Func<CameraEvent, Task> eventHandler, CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogInformation($"Received message: {message}");
                
                var cameraEvent = JsonSerializer.Deserialize<CameraEvent>(message);
                if (cameraEvent != null)
                {
                    await eventHandler(cameraEvent);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize camera event");
                }
                
                // Acknowledge the message
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                _logger.LogDebug($"Message acknowledged: {ea.DeliveryTag}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                // Reject and requeue the message
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        // Set QoS to process one message at a time
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        
        var consumerTag = _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
        
        _logger.LogInformation($"Started consuming messages with consumer tag: {consumerTag}");
        
        // Keep the consumer running
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken);
                
                // Periodically check queue status
                try
                {
                    var queueInfo = _channel.QueueDeclarePassive(QueueName);
                    if (queueInfo.MessageCount > 0)
                    {
                        _logger.LogInformation($"Queue status: {queueInfo.MessageCount} messages pending");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check queue status");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer stopped due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in consumer loop");
        }
        finally
        {
            try
            {
                _channel.BasicCancel(consumerTag);
                _logger.LogInformation("Consumer cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling consumer");
            }
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}

// NVR1 Service
public class NVR1Service : BackgroundService
{
    private readonly IRabbitMQConsumer _consumer;
    private readonly ILogger<NVR1Service> _logger;

    public NVR1Service(IRabbitMQConsumer consumer, ILogger<NVR1Service> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NVR1Service started and waiting for camera events");
        
        await _consumer.StartConsumingAsync(ProcessCameraEvent, stoppingToken);
    }

    private async Task ProcessCameraEvent(CameraEvent cameraEvent)
    {
        _logger.LogInformation($"Processing camera event: Camera={cameraEvent.CameraId}, Type={cameraEvent.EventType}, Time={cameraEvent.Timestamp}");
        
        // Simulate processing time
        await Task.Delay(1000);
        
        // Here you would implement your actual business logic for NVR processing
        switch (cameraEvent.EventType)
        {
            case "MotionDetected":
                await HandleMotionDetection(cameraEvent);
                break;
            case "AudioAlert":
                await HandleAudioAlert(cameraEvent);
                break;
            case "ObjectDetection":
                await HandleObjectDetection(cameraEvent);
                break;
            case "TamperAlert":
                await HandleTamperAlert(cameraEvent);
                break;
            default:
                _logger.LogWarning($"Unknown event type: {cameraEvent.EventType}");
                break;
        }
    }

    private async Task HandleMotionDetection(CameraEvent cameraEvent)
    {
        _logger.LogInformation($"NVR1: Starting recording for motion detection from {cameraEvent.CameraId}");
        // Implement motion detection logic
        await Task.CompletedTask;
    }

    private async Task HandleAudioAlert(CameraEvent cameraEvent)
    {
        _logger.LogInformation($"NVR1: Processing audio alert from {cameraEvent.CameraId}");
        // Implement audio alert logic
        await Task.CompletedTask;
    }

    private async Task HandleObjectDetection(CameraEvent cameraEvent)
    {
        _logger.LogInformation($"NVR1: Analyzing object detection from {cameraEvent.CameraId}");
        // Implement object detection logic
        await Task.CompletedTask;
    }

    private async Task HandleTamperAlert(CameraEvent cameraEvent)
    {
        _logger.LogInformation($"NVR1: CRITICAL - Tamper alert from {cameraEvent.CameraId}");
        // Implement tamper alert logic
        await Task.CompletedTask;
    }
}