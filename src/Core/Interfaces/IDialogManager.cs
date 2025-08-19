using VoiceAgent.Models.Dialog;
using VoiceAgent.Models.Requests;

namespace VoiceAgent.Core.Interfaces;

/// <summary>
/// Core dialog management interface handling conversation flow and state transitions.
/// Implements healthcare-specific slot filling and intent classification.
/// </summary>
public interface IDialogManager
{
    /// <summary>
    /// Processes incoming caller input and determines next conversation step
    /// </summary>
    /// <param name="callSid">Unique call identifier for state correlation</param>
    /// <param name="input">Caller input (speech or DTMF)</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <returns>Dialog response with next actions and TwiML</returns>
    Task<DialogResponse> ProcessTurnAsync(
        string callSid, 
        string input, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates new conversation with HIPAA-compliant greeting and consent
    /// </summary>
    /// <param name="callSid">Unique call identifier</param>
    /// <param name="callerNumber">Incoming phone number (redacted in logs)</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <returns>Initial dialog response with greeting and consent request</returns>
    Task<DialogResponse> InitiateConversationAsync(
        string callSid, 
        string callerNumber, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles conversation completion with handoff or escalation
    /// </summary>
    /// <param name="callSid">Unique call identifier</param>
    /// <param name="completionType">Type of completion (success, escalation, timeout)</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <returns>Final dialog response with completion actions</returns>
    Task<DialogResponse> CompleteConversationAsync(
        string callSid, 
        ConversationCompletionType completionType, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current conversation state for diagnostics and evaluation
    /// </summary>
    /// <param name="callSid">Unique call identifier</param>
    /// <returns>Current conversation state and collected slots</returns>
    Task<ConversationState> GetConversationStateAsync(string callSid);

    /// <summary>
    /// Validates conversation readiness for handoff to scheduling system
    /// </summary>
    /// <param name="callSid">Unique call identifier</param>
    /// <returns>Validation result with missing fields and quality score</returns>
    Task<HandoffValidationResult> ValidateHandoffReadinessAsync(string callSid);
}

/// <summary>
/// Conversation completion types for metrics and evaluation
/// </summary>
public enum ConversationCompletionType
{
    /// <summary>Successful completion with all required slots filled</summary>
    Success,
    
    /// <summary>Escalation to human agent requested by caller or system</summary>
    Escalation,
    
    /// <summary>Conversation timeout due to inactivity or duration limits</summary>
    Timeout,
    
    /// <summary>Technical error requiring conversation termination</summary>
    TechnicalError,
    
    /// <summary>Caller disconnected during conversation</summary>
    CallerDisconnect
}
