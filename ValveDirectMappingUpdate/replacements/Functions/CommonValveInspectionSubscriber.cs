using System.Text.Json;
using Azure.Messaging.ServiceBus;
using GaValveInspectionGisMaximo.Configuration;
using GaValveInspectionGisMaximo.Contracts;
using GaValveInspectionGisMaximo.Serialization;
using GaValveInspectionGisMaximo.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaValveInspectionGisMaximo.Functions;

public sealed class CommonValveInspectionSubscriber
{
   private const string MessageTypeProperty = "messageType";
   private const string CorrelationIdProperty = "x-correlation-id";

   private const int MaximumDeadLetterDescriptionLength = 4_096;
   private readonly DownstreamOptions _options;
   private readonly ILogger<CommonValveInspectionSubscriber> _logger;

   public CommonValveInspectionSubscriber(
       IOptions<DownstreamOptions> options,
       ILogger<CommonValveInspectionSubscriber> logger)
   {
       ArgumentNullException.ThrowIfNull(options);
       ArgumentNullException.ThrowIfNull(options.Value);
       ArgumentNullException.ThrowIfNull(logger);
       _options = options.Value;
       _logger = logger;
   }

   [Function(FunctionNames.CommonValveInspectionSubscriber)]
   public async Task RunAsync(
       [ServiceBusTrigger(
           "%ServiceBusTopicName%",
           "%ServiceBusSubscriptionName%",
           Connection = "ServiceBusConnection",
           AutoCompleteMessages = false)]
       ServiceBusReceivedMessage receivedMessage,
       ServiceBusMessageActions messageActions,
       [DurableClient] DurableTaskClient durableClient,
       CancellationToken cancellationToken)
   {
       ArgumentNullException.ThrowIfNull(receivedMessage);
       ArgumentNullException.ThrowIfNull(messageActions);
       ArgumentNullException.ThrowIfNull(durableClient);

       var propertyMessageType = GetPropertyValue(
           receivedMessage,
           MessageTypeProperty);

       _logger.LogInformation(
           "Received valve inspection message. " +
           "MessageId={MessageId}, SequenceNumber={SequenceNumber}, " +
           "DeliveryCount={DeliveryCount}, " +
           "CorrelationId={CorrelationId}, " +
           "PropertyMessageType={PropertyMessageType}, " +
           "Subject={Subject}",
           receivedMessage.MessageId,
           receivedMessage.SequenceNumber,
           receivedMessage.DeliveryCount,
           receivedMessage.CorrelationId ?? "<missing>",
           propertyMessageType ?? "<missing>",
           receivedMessage.Subject ?? "<missing>");

       ValveInspectionMessage? message;

       try
       {
           message = JsonSerializer.Deserialize<ValveInspectionMessage>(
               receivedMessage.Body.ToMemory().Span,
               JsonDefaults.Options);
       }
       catch (Exception exception)
           when (exception is JsonException or NotSupportedException)
       {
           _logger.LogWarning(
               exception,
               "Unable to deserialize valve inspection message. " +
               "MessageId={MessageId}, SequenceNumber={SequenceNumber}",
               receivedMessage.MessageId,
               receivedMessage.SequenceNumber);

           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "InvalidJson",
                   "The message body is not a valid valve inspection message.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (message is null)
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "EmptyEnvelope",
                   "A valve inspection message envelope is required.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (message.Payload is null)
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "EmptyPayload",
                   "A valve inspection payload is required.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       var correlationId = ResolveCorrelationId(
           message.CorrelationId,
           receivedMessage);

       if (!MessageTypes.TryNormalize(
               propertyMessageType,
               out var normalizedPropertyMessageType))
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "InvalidRouting",
                   $"The messageType application property " +
                   $"'{propertyMessageType ?? "<missing>"}' is unsupported. " +
                   $"Supported values are '{MessageTypes.GisToMaximo}' and " +
                   $"'{MessageTypes.MaximoToGis}'.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (!MessageTypes.TryNormalize(
               message.MessageType,
               out var normalizedBodyMessageType))
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "InvalidBodyMessageType",
                   $"The message body messageType " +
                   $"'{message.MessageType ?? "<missing>"}' is unsupported. " +
                   $"Supported values are '{MessageTypes.GisToMaximo}' and " +
                   $"'{MessageTypes.MaximoToGis}'.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (!string.Equals(
               normalizedPropertyMessageType,
               normalizedBodyMessageType,
               StringComparison.Ordinal))
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "MessageTypeMismatch",
                   $"The Service Bus application property messageType " +
                   $"'{normalizedPropertyMessageType}' does not match the " +
                   $"body messageType '{normalizedBodyMessageType}'.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (!TryValidateSubject(
               receivedMessage.Subject,
               normalizedBodyMessageType,
               out var subjectError))
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "SubjectMismatch",
                   subjectError,
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       if (!TryResolveRoute(
               normalizedBodyMessageType,
               out var source,
               out var target,
               out var orchestratorName))
       {
           await DeadLetterAsync(
                   receivedMessage,
                   messageActions,
                   "InvalidRouting",
                   $"No orchestration route is configured for " +
                   $"messageType '{normalizedBodyMessageType}'.",
                   cancellationToken)
               .ConfigureAwait(false);

           return;
       }

       _logger.LogInformation(
           "Resolved valve inspection routing. " +
           "MessageId={MessageId}, CorrelationId={CorrelationId}, " +
           "BodyMessageType={BodyMessageType}, " +
           "PropertyMessageType={PropertyMessageType}, " +
           "Subject={Subject}, Source={Source}, Target={Target}, " +
           "Orchestrator={Orchestrator}",
           receivedMessage.MessageId,
           correlationId,
           normalizedBodyMessageType,
           normalizedPropertyMessageType,
           receivedMessage.Subject ?? "<missing>",
           source,
           target,
           orchestratorName);

       var orchestrationMessage = message with
       {
           MessageType = normalizedBodyMessageType,
           CorrelationId = correlationId,
           Payload = message.Payload,
           ReceivedAtUtc =
               message.ReceivedAtUtc == default
                   ? DateTimeOffset.UtcNow
                   : message.ReceivedAtUtc,
           RetryPolicy = CreateRetryPolicy()
       };

       string instanceId;

       try
       {
           instanceId = await durableClient
               .ScheduleNewOrchestrationInstanceAsync(
                   orchestratorName,
                   orchestrationMessage,
                   cancellationToken)
               .ConfigureAwait(false);
       }
       catch (Exception exception)
       {
           _logger.LogError(
               exception,
               "Failed to schedule valve inspection orchestration. " +
               "MessageId={MessageId}, CorrelationId={CorrelationId}, " +
               "MessageType={MessageType}, " +
               "Orchestrator={Orchestrator}",
               receivedMessage.MessageId,
               correlationId,
               normalizedBodyMessageType,
               orchestratorName);

           // Do not complete or dead-letter the message here.
           // Rethrowing allows Service Bus retry processing.
           throw;
       }

       _logger.LogInformation(
           "Started valve inspection orchestration. " +
           "InstanceId={InstanceId}, MessageId={MessageId}, " +
           "SequenceNumber={SequenceNumber}, " +
           "MessageType={MessageType}, Source={Source}, " +
           "Target={Target}, Action={Action}, " +
           "CorrelationId={CorrelationId}",
           instanceId,
           receivedMessage.MessageId,
           receivedMessage.SequenceNumber,
           normalizedBodyMessageType,
           source,
           target,
           message.Payload.Action,
           correlationId);

       await messageActions
           .CompleteMessageAsync(
               receivedMessage,
               cancellationToken)
           .ConfigureAwait(false);

       _logger.LogInformation(
           "Completed Service Bus message. " +
           "MessageId={MessageId}, InstanceId={InstanceId}, " +
           "MessageType={MessageType}, CorrelationId={CorrelationId}",
           receivedMessage.MessageId,
           instanceId,
           normalizedBodyMessageType,
           correlationId);
   }

   private WorkflowRetryPolicy CreateRetryPolicy()
   {
       return new WorkflowRetryPolicy(
           MaxAttempts: _options.MaxAttempts,
           FirstDelaySeconds: _options.FirstDelaySeconds,
           BackoffCoefficient: _options.BackoffCoefficient,
           MaxDelaySeconds: _options.MaxDelaySeconds,
           TimeoutSeconds: _options.RetryTimeoutSeconds);
   }

   private static bool TryResolveRoute(
       string messageType,
       out string source,
       out string target,
       out string orchestratorName)
   {
       source = string.Empty;
       target = string.Empty;
       orchestratorName = string.Empty;

       switch (messageType)
       {
           case MessageTypes.GisToMaximo:
               source = "GIS";
               target = "Maximo";
               orchestratorName =
                   FunctionNames.GisToMaximoOrchestrator;
               return true;

           case MessageTypes.MaximoToGis:
               source = "Maximo";
               target = "GIS";
               orchestratorName =
                   FunctionNames.MaximoToGisOrchestrator;
               return true;

           default:
               return false;
       }
   }

   private static bool TryValidateSubject(
       string? subject,
       string expectedMessageType,
       out string error)
   {
       error = string.Empty;

       // Subject is optional. APIM currently sets it through
       // BrokerProperties.Label.
       if (string.IsNullOrWhiteSpace(subject))
       {
           return true;
       }

       if (!MessageTypes.TryNormalize(
               subject,
               out var normalizedSubject))
       {
           error =
               $"The Service Bus subject '{subject}' is unsupported.";

           return false;
       }

       if (!string.Equals(
               normalizedSubject,
               expectedMessageType,
               StringComparison.Ordinal))
       {
           error =
               $"The Service Bus subject '{normalizedSubject}' does not " +
               $"match the body messageType '{expectedMessageType}'.";

           return false;
       }

       return true;
   }

   private static string? GetPropertyValue(
       ServiceBusReceivedMessage message,
       string propertyName)
   {
       ArgumentNullException.ThrowIfNull(message);
       ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

       foreach (var property in message.ApplicationProperties)
       {
           if (!string.Equals(
                   property.Key,
                   propertyName,
                   StringComparison.OrdinalIgnoreCase))
           {
               continue;
           }

           var value = Convert.ToString(property.Value)?.Trim();

           return string.IsNullOrWhiteSpace(value)
               ? null
               : value;
       }

       return null;
   }

   private static string ResolveCorrelationId(
       string? bodyCorrelationId,
       ServiceBusReceivedMessage receivedMessage)
   {
       if (!string.IsNullOrWhiteSpace(bodyCorrelationId))
       {
           return bodyCorrelationId.Trim();
       }

       if (!string.IsNullOrWhiteSpace(
               receivedMessage.CorrelationId))
       {
           return receivedMessage.CorrelationId.Trim();
       }

       var applicationPropertyCorrelationId =
           GetPropertyValue(
               receivedMessage,
               CorrelationIdProperty);

       if (!string.IsNullOrWhiteSpace(
               applicationPropertyCorrelationId))
       {
           return applicationPropertyCorrelationId;
       }

       return Guid.NewGuid().ToString("D");
   }

   private async Task DeadLetterAsync(
       ServiceBusReceivedMessage message,
       ServiceBusMessageActions actions,
       string reason,
       string description,
       CancellationToken cancellationToken)
   {
       var normalizedDescription = Truncate(
           description,
           MaximumDeadLetterDescriptionLength);

       _logger.LogWarning(
           "Dead-lettering valve inspection message. " +
           "MessageId={MessageId}, SequenceNumber={SequenceNumber}, " +
           "DeliveryCount={DeliveryCount}, CorrelationId={CorrelationId}, " +
           "PropertyMessageType={PropertyMessageType}, " +
           "BodySubject={Subject}, Reason={Reason}, " +
           "Description={Description}",
           message.MessageId,
           message.SequenceNumber,
           message.DeliveryCount,
           message.CorrelationId ?? "<missing>",
           GetPropertyValue(message, MessageTypeProperty) ?? "<missing>",
           message.Subject ?? "<missing>",
           reason,
           normalizedDescription);

       await actions
           .DeadLetterMessageAsync(
               message,
               deadLetterReason: reason,
               deadLetterErrorDescription: normalizedDescription,
               cancellationToken: cancellationToken)
           .ConfigureAwait(false);
   }

   private static string Truncate(
       string value,
       int maximumLength)
   {
       ArgumentNullException.ThrowIfNull(value);

       return value.Length <= maximumLength
           ? value
           : value[..maximumLength];
   }
}
