using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VoiceAgent.Models.Dialog;

/// <summary>
/// Represents the complete conversation state including collected slots and metadata.
/// Implements PHI-aware serialization and validation for healthcare compliance.
/// </summary>
public class ConversationState
{
    /// <summary>Unique conversation identifier</summary>
    public string CallSid { get; set; } = string.Empty;
    
    /// <summary>Current conversation phase</summary>
    public ConversationPhase Phase { get; set; }
    
    /// <summary>Collected appointment booking information</summary>
    public AppointmentSlots Slots { get; set; } = new();
    
    /// <summary>Conversation metadata and quality metrics</summary>
    public ConversationMetadata Metadata { get; set; } = new();
    
    /// <summary>HIPAA consent and disclosure status</summary>
    public ConsentStatus Consent { get; set; } = new();
    
    /// <summary>Current conversation confidence and validation status</summary>
    public ValidationStatus Validation { get; set; } = new();
}

/// <summary>
/// Healthcare appointment booking slots with validation and PHI protection
/// </summary>
public class AppointmentSlots
{
    /// <summary>Patient name or callback preference</summary>
    [PersonalData]
    public string? PatientName { get; set; }
    
    /// <summary>Callback phone number if name not provided</summary>
    [PersonalData]
    [Phone]
    public string? CallbackNumber { get; set; }
    
    /// <summary>Date of birth for verification (optional based on policy)</summary>
    [PersonalData]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }
    
    /// <summary>Preferred provider or specialty</summary>
    public string? PreferredProvider { get; set; }
    
    /// <summary>Insurance plan information (normalized downstream)</summary>
    public string? InsurancePlan { get; set; }
    
    /// <summary>Appointment type or reason for visit</summary>
    public string? AppointmentType { get; set; }
    
    /// <summary>Preferred appointment date/time range</summary>
    public PreferredSchedule? PreferredSchedule { get; set; }
    
    /// <summary>Special accommodations or requirements</summary>
    public string? SpecialRequirements { get; set; }
    
    /// <summary>Validates completeness of required slots for handoff</summary>
    public bool IsReadyForHandoff()
    {
        // Implementation abstracted - would validate required fields
        // based on practice-specific configuration and policies
        throw new NotImplementedException("Validation logic abstracted for showcase");
    }
    
    /// <summary>Generates redacted summary for logging and audit</summary>
    public string GetRedactedSummary()
    {
        // Implementation abstracted - would create PHI-safe summary
        throw new NotImplementedException("Redaction logic abstracted for showcase");
    }
}

/// <summary>
/// Preferred scheduling information with flexible date/time handling
/// </summary>
public class PreferredSchedule
{
    /// <summary>Preferred appointment date</summary>
    public DateOnly? PreferredDate { get; set; }
    
    /// <summary>Preferred time of day</summary>
    public TimeOfDay? PreferredTimeOfDay { get; set; }
    
    /// <summary>Acceptable date range for scheduling</summary>
    public DateRange? AcceptableDateRange { get; set; }
    
    /// <summary>Days of week availability</summary>
    public DayOfWeek[]? AvailableDaysOfWeek { get; set; }
    
    /// <summary>Urgency level for appointment scheduling</summary>
    public UrgencyLevel UrgencyLevel { get; set; } = UrgencyLevel.Routine;
}

/// <summary>
/// Time of day preferences for appointment scheduling
/// </summary>
public enum TimeOfDay
{
    /// <summary>No specific preference</summary>
    NoPreference,
    
    /// <summary>Morning appointments (8 AM - 12 PM)</summary>
    Morning,
    
    /// <summary>Afternoon appointments (12 PM - 5 PM)</summary>
    Afternoon,
    
    /// <summary>Evening appointments (5 PM - 8 PM)</summary>
    Evening,
    
    /// <summary>Earliest available appointment</summary>
    EarliestAvailable
}

/// <summary>
/// Appointment urgency levels for prioritization
/// </summary>
public enum UrgencyLevel
{
    /// <summary>Routine appointment, standard scheduling</summary>
    Routine,
    
    /// <summary>Preferred within 1-2 weeks</summary>
    SoonPreferred,
    
    /// <summary>Urgent, within 1 week if possible</summary>
    Urgent,
    
    /// <summary>Emergency consultation required</summary>
    Emergency
}

/// <summary>
/// Date range specification for flexible scheduling
/// </summary>
public class DateRange
{
    /// <summary>Earliest acceptable date</summary>
    public DateOnly StartDate { get; set; }
    
    /// <summary>Latest acceptable date</summary>
    public DateOnly EndDate { get; set; }
    
    /// <summary>Validates date range is logical and reasonable</summary>
    public bool IsValid => StartDate <= EndDate && StartDate >= DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>
/// Conversation phases for state machine management
/// </summary>
public enum ConversationPhase
{
    /// <summary>Initial greeting and consent collection</summary>
    Greeting,
    
    /// <summary>Intent classification (book, confirm, cancel)</summary>
    IntentClassification,
    
    /// <summary>Slot filling for appointment details</summary>
    SlotFilling,
    
    /// <summary>Confirmation and validation of collected information</summary>
    Confirmation,
    
    /// <summary>Handoff to scheduling system</summary>
    Handoff,
    
    /// <summary>Escalation to human agent</summary>
    Escalation,
    
    /// <summary>Conversation completion</summary>
    Completion
}

/// <summary>
/// HIPAA consent and disclosure tracking
/// </summary>
public class ConsentStatus
{
    /// <summary>Recording consent obtained</summary>
    public bool RecordingConsentObtained { get; set; }
    
    /// <summary>Timestamp when consent was obtained</summary>
    public DateTime? ConsentTimestamp { get; set; }
    
    /// <summary>PHI processing consent for appointment booking</summary>
    public bool PhiProcessingConsent { get; set; }
    
    /// <summary>Communication preferences for follow-up</summary>
    public CommunicationPreferences? CommunicationPreferences { get; set; }
}

/// <summary>
/// Communication preferences for healthcare follow-up
/// </summary>
public class CommunicationPreferences
{
    /// <summary>Preferred contact method</summary>
    public ContactMethod PreferredContactMethod { get; set; }
    
    /// <summary>Acceptable contact times</summary>
    public TimeSpan[]? AcceptableContactTimes { get; set; }
    
    /// <summary>Language preference for communication</summary>
    public string? LanguagePreference { get; set; }
}

/// <summary>
/// Contact methods for healthcare communication
/// </summary>
public enum ContactMethod
{
    /// <summary>Phone call communication</summary>
    Phone,
    
    /// <summary>SMS text messaging</summary>
    SMS,
    
    /// <summary>Email communication</summary>
    Email,
    
    /// <summary>Patient portal messaging</summary>
    PatientPortal,
    
    /// <summary>No follow-up communication needed</summary>
    None
}

/// <summary>
/// Conversation validation status and quality metrics
/// </summary>
public class ValidationStatus
{
    /// <summary>Overall conversation confidence score (0.0 - 1.0)</summary>
    public double ConfidenceScore { get; set; }
    
    /// <summary>Missing required fields for handoff</summary>
    public string[] MissingRequiredFields { get; set; } = Array.Empty<string>();
    
    /// <summary>Validation warnings and potential issues</summary>
    public ValidationWarning[] Warnings { get; set; } = Array.Empty<ValidationWarning>();
    
    /// <summary>Slot filling quality assessment</summary>
    public SlotQualityMetrics SlotQuality { get; set; } = new();
    
    /// <summary>Readiness for handoff to scheduling system</summary>
    public bool IsReadyForHandoff => MissingRequiredFields.Length == 0 && ConfidenceScore >= 0.8;
}

/// <summary>
/// Validation warnings for conversation quality assessment
/// </summary>
public class ValidationWarning
{
    /// <summary>Warning severity level</summary>
    public WarningSeverity Severity { get; set; }
    
    /// <summary>Warning message for operators</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Affected slot or conversation aspect</summary>
    public string? AffectedField { get; set; }
    
    /// <summary>Recommended action to resolve warning</summary>
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// Warning severity levels for validation issues
/// </summary>
public enum WarningSeverity
{
    /// <summary>Informational note, no action required</summary>
    Info,
    
    /// <summary>Minor issue, may affect quality</summary>
    Low,
    
    /// <summary>Moderate issue, should be addressed</summary>
    Medium,
    
    /// <summary>High-priority issue, likely to cause problems</summary>
    High,
    
    /// <summary>Critical issue, handoff not recommended</summary>
    Critical
}

/// <summary>
/// Quality metrics for slot filling assessment
/// </summary>
public class SlotQualityMetrics
{
    /// <summary>Percentage of slots filled correctly on first attempt</summary>
    public double FirstTurnAccuracy { get; set; }
    
    /// <summary>Average confidence score across all filled slots</summary>
    public double AverageSlotConfidence { get; set; }
    
    /// <summary>Number of clarification questions required</summary>
    public int ClarificationTurns { get; set; }
    
    /// <summary>Overall conversation efficiency score</summary>
    public double EfficiencyScore { get; set; }
}

/// <summary>
/// Conversation metadata for evaluation and monitoring
/// </summary>
public class ConversationMetadata
{
    /// <summary>Conversation start timestamp</summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>Last activity timestamp</summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary>Total conversation duration</summary>
    public TimeSpan Duration => LastActivity - StartTime;
    
    /// <summary>Number of turns in conversation</summary>
    public int TurnCount { get; set; }
    
    /// <summary>LLM models used during conversation</summary>
    public ModelUsageMetrics[] ModelUsage { get; set; } = Array.Empty<ModelUsageMetrics>();
    
    /// <summary>Conversation outcome classification</summary>
    public ConversationOutcome Outcome { get; set; }
    
    /// <summary>Quality assessment scores</summary>
    public QualityAssessment Quality { get; set; } = new();
}

/// <summary>
/// LLM model usage tracking for cost and performance analysis
/// </summary>
public class ModelUsageMetrics
{
    /// <summary>Model identifier (e.g., gpt-4o-mini, claude-3-sonnet)</summary>
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>Provider name (Azure, AWS, Google)</summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>Number of turns using this model</summary>
    public int TurnCount { get; set; }
    
    /// <summary>Total tokens consumed</summary>
    public int TokensUsed { get; set; }
    
    /// <summary>Estimated cost for this model usage</summary>
    public decimal EstimatedCost { get; set; }
    
    /// <summary>Average response time for this model</summary>
    public TimeSpan AverageResponseTime { get; set; }
}

/// <summary>
/// Conversation outcome classifications for metrics and evaluation
/// </summary>
public enum ConversationOutcome
{
    /// <summary>Successful completion with handoff</summary>
    SuccessfulHandoff,
    
    /// <summary>Escalated to human agent</summary>
    EscalatedToHuman,
    
    /// <summary>Caller requested callback</summary>
    CallbackRequested,
    
    /// <summary>Conversation timed out</summary>
    Timeout,
    
    /// <summary>Technical error occurred</summary>
    TechnicalError,
    
    /// <summary>Caller disconnected</summary>
    CallerDisconnect,
    
    /// <summary>Inappropriate content detected</summary>
    ContentViolation
}

/// <summary>
/// Quality assessment metrics for conversation evaluation
/// </summary>
public class QualityAssessment
{
    /// <summary>Overall conversation quality score (0.0 - 1.0)</summary>
    public double OverallScore { get; set; }
    
    /// <summary>Information gathering effectiveness</summary>
    public double InformationGatheringScore { get; set; }
    
    /// <summary>Natural language interaction quality</summary>
    public double ConversationFlowScore { get; set; }
    
    /// <summary>Compliance with healthcare protocols</summary>
    public double ComplianceScore { get; set; }
    
    /// <summary>Error handling and recovery effectiveness</summary>
    public double ErrorHandlingScore { get; set; }
}

/// <summary>
/// Custom attribute for marking personally identifiable information
/// Used by redaction and compliance systems
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class PersonalDataAttribute : Attribute
{
    /// <summary>Type of personal data for specialized handling</summary>
    public PersonalDataType DataType { get; set; } = PersonalDataType.General;
    
    /// <summary>Redaction strategy for this data type</summary>
    public RedactionStrategy RedactionStrategy { get; set; } = RedactionStrategy.Hash;
}

/// <summary>
/// Types of personal data for specialized handling
/// </summary>
public enum PersonalDataType
{
    /// <summary>General personal information</summary>
    General,
    
    /// <summary>Protected Health Information (PHI)</summary>
    PHI,
    
    /// <summary>Personally Identifiable Information (PII)</summary>
    PII,
    
    /// <summary>Financial information</summary>
    Financial,
    
    /// <summary>Biometric data</summary>
    Biometric
}

/// <summary>
/// Redaction strategies for different data types
/// </summary>
public enum RedactionStrategy
{
    /// <summary>Replace with hash for consistent redaction</summary>
    Hash,
    
    /// <summary>Replace with asterisks or placeholder</summary>
    Mask,
    
    /// <summary>Remove entirely from output</summary>
    Remove,
    
    /// <summary>Encrypt for authorized access only</summary>
    Encrypt,
    
    /// <summary>Tokenize for referential integrity</summary>
    Tokenize
}
